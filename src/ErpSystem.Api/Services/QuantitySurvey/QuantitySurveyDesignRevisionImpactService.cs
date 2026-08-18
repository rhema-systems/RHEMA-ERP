using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ErpSystem.Core.DTOs.QuantitySurvey;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Entities.QuantitySurvey;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Interfaces.QuantitySurvey;
using ErpSystem.Core.Services.QuantitySurvey;
using ErpSystem.Core.Services.Workflow;
using ErpSystem.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.QuantitySurvey;

public sealed class QuantitySurveyDesignRevisionImpactService(
    ApplicationDbContext db,
    ICurrentUserService currentUser,
    IProjectService projectService,
    IWorkflowIntegrationService workflow,
    IWorkflowStatusAdapterRegistry workflowAdapters) : IQuantitySurveyDesignRevisionImpactService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private Guid TenantId => currentUser.TenantId is { } value && value != Guid.Empty
        ? value : throw new UnauthorizedAccessException("A valid tenant context is required.");
    private Guid UserId => Guid.TryParse(currentUser.UserId, out var value) && value != Guid.Empty
        ? value : throw new UnauthorizedAccessException("An authenticated user is required.");
    private string UserName => string.IsNullOrWhiteSpace(currentUser.UserName) ? UserId.ToString() : currentUser.UserName.Trim();
    private string ActorRoles => string.Join(",", currentUser.Roles.Where(value => !string.IsNullOrWhiteSpace(value))
        .Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value));

    public async Task<QuantitySurveyDesignImpactLookupsDto> GetLookupsAsync(Guid projectId, CancellationToken token = default)
    {
        await RequireProjectAsync(projectId);
        var drawings = await db.ProjectDrawings.AsNoTracking()
            .Where(value => value.TenantId == TenantId && value.ProjectId == projectId && !value.IsDeleted &&
                            value.Revision != null &&
                            (value.Status == ProjectDrawingStatuses.ApprovedForConstruction ||
                             value.Status == ProjectDrawingStatuses.ApprovedAsBuilt ||
                             value.Status == ProjectDrawingStatuses.Superseded))
            .OrderBy(value => value.DrawingNumber).ThenBy(value => value.IssuedDate).ThenBy(value => value.Revision)
            .Select(value => new QuantitySurveyDesignImpactLookupDto
            {
                Id = value.Id,
                Label = value.DrawingNumber + " · Rev " + value.Revision,
                Group = value.Discipline,
                Description = value.Title + " · " + value.Status,
                SupersedesDrawingId = value.SupersedesDrawingId
            }).ToListAsync(token);
        var lines = await db.ProjectBoqVersionLines.AsNoTracking().Include(value => value.Version)
            .Where(value => value.TenantId == TenantId && value.ProjectId == projectId && !value.IsDeleted &&
                            value.ItemType == ProjectBoqItemTypes.Item && !value.Version.IsDeleted &&
                            value.Version.Status == ProjectBoqVersionStatuses.Approved && value.Version.PublishedAt != null)
            .OrderByDescending(value => value.Version.VersionNumber).ThenBy(value => value.SortOrder)
            .Select(value => new QuantitySurveyDesignImpactLookupDto
            {
                Id = value.Id,
                Label = (value.LineNumber ?? value.ItemCode ?? "Item") + " · " + value.Description,
                Group = "Approved BoQ v" + value.Version.VersionNumber,
                Description = "Quantity " + value.Quantity + " · " + (value.UnitOfMeasure ?? "No unit")
            }).ToListAsync(token);
        return new() { Drawings = drawings, ApprovedBoqLines = lines };
    }

    public async Task<IReadOnlyList<QuantitySurveyDesignImpactDto>> ListAsync(Guid projectId, CancellationToken token = default)
    {
        await RequireProjectAsync(projectId);
        return (await db.QuantitySurveyDesignRevisionImpacts.AsNoTracking()
            .Include(value => value.Lines)
            .Where(value => value.TenantId == TenantId && value.ProjectId == projectId && !value.IsDeleted)
            .OrderByDescending(value => value.CreatedAt).ToListAsync(token)).Select(Map).ToList();
    }

    public async Task<QuantitySurveyDesignImpactDto> GetAsync(Guid id, CancellationToken token = default)
    {
        var entity = await RequiredAsync(id, false, token);
        await RequireProjectAsync(entity.ProjectId);
        return Map(entity);
    }

    public async Task<QuantitySurveyDesignImpactDto> CreateAsync(
        CreateQuantitySurveyDesignImpactRequest request, string correlationId, CancellationToken token = default)
    {
        if (request.ClientRequestId == Guid.Empty || request.ProjectId == Guid.Empty ||
            request.PreviousDrawingId == Guid.Empty || request.RevisedDrawingId == Guid.Empty)
            throw Validation("Select the project, prior drawing revision and revised drawing, and provide a client request identifier.");
        if (!Enum.IsDefined(request.Route)) throw Validation("Select a controlled workflow route.");
        var title = RequiredText(request.Title, 3, 200, "Title");
        var summary = RequiredText(request.ChangeSummary, 10, 2000, "Change summary");
        if (request.Lines.Count == 0 || request.Lines.Count > 200)
            throw Validation("Select between 1 and 200 affected approved BoQ lines.");
        if (request.Lines.Any(value => value.ProjectBoqVersionLineId == Guid.Empty || !Enum.IsDefined(value.ImpactType)) ||
            request.Lines.Select(value => value.ProjectBoqVersionLineId).Distinct().Count() != request.Lines.Count)
            throw Validation("Affected BoQ lines must be unique and use controlled impact types.");
        await RequireProjectAsync(request.ProjectId);
        var requestHash = Hash(new
        {
            request.ProjectId, request.PreviousDrawingId, request.RevisedDrawingId, request.Route, Title = title,
            Summary = summary,
            Lines = request.Lines.OrderBy(value => value.ProjectBoqVersionLineId).Select(value => new
            { value.ProjectBoqVersionLineId, value.ImpactType, value.IndicativeQuantity, Reason = value.ImpactReason.Trim() })
        });
        var retry = await db.QuantitySurveyDesignRevisionImpacts.AsNoTracking().Include(value => value.Lines)
            .FirstOrDefaultAsync(value => value.TenantId == TenantId && value.ClientRequestId == request.ClientRequestId, token);
        if (retry is not null)
        {
            if (!FixedEquals(retry.RequestHash, requestHash)) throw RetryConflict();
            return Map(retry);
        }

        var drawings = await db.ProjectDrawings.AsNoTracking().Where(value => value.TenantId == TenantId &&
                value.ProjectId == request.ProjectId && !value.IsDeleted &&
                (value.Id == request.PreviousDrawingId || value.Id == request.RevisedDrawingId))
            .ToListAsync(token);
        var previous = drawings.SingleOrDefault(value => value.Id == request.PreviousDrawingId)
            ?? throw Validation("The selected prior drawing revision is unavailable.");
        var revised = drawings.SingleOrDefault(value => value.Id == request.RevisedDrawingId)
            ?? throw Validation("The selected revised drawing is unavailable.");
        ValidateDrawingLineage(previous, revised);

        var lineIds = request.Lines.Select(value => value.ProjectBoqVersionLineId).ToList();
        var lines = await db.ProjectBoqVersionLines.AsNoTracking().Include(value => value.Version)
            .Where(value => value.TenantId == TenantId && lineIds.Contains(value.Id) && !value.IsDeleted &&
                            value.ProjectId == request.ProjectId && value.ItemType == ProjectBoqItemTypes.Item &&
                            !value.Version.IsDeleted && value.Version.Status == ProjectBoqVersionStatuses.Approved &&
                            value.Version.PublishedAt != null).ToListAsync(token);
        if (lines.Count != lineIds.Count) throw Validation("Every affected line must belong to a published approved BoQ for this project.");
        var policy = await ResolvePolicyAsync(request.Route, DateTime.UtcNow, token);
        var entity = new QuantitySurveyDesignRevisionImpact
        {
            Id = Guid.NewGuid(), TenantId = TenantId, ProjectId = request.ProjectId,
            PreviousDrawingId = previous.Id, RevisedDrawingId = revised.Id,
            ClientRequestId = request.ClientRequestId, RequestHash = requestHash,
            ImpactNumber = $"DSI-{DateTime.UtcNow:yyMMdd}-{request.ClientRequestId:N}"[..19].ToUpperInvariant(),
            Title = title, ChangeSummary = summary, Route = request.Route,
            ConfigurationProfileId = policy.ProfileId, ConfigurationDecisionId = policy.DecisionId,
            ApprovalWorkflowDefinitionId = policy.WorkflowDefinitionId,
            WorkflowEntityTypeCode = policy.WorkflowEntityTypeCode, PolicyHash = policy.PolicyHash,
            PreviousDrawingNumberSnapshot = previous.DrawingNumber, PreviousRevisionSnapshot = previous.Revision,
            RevisedDrawingNumberSnapshot = revised.DrawingNumber, RevisedRevisionSnapshot = revised.Revision!,
            CreatedByUserId = UserId, AuditAction = QuantitySurveyAuditEventMap.CreateDesignRevisionImpact,
            CorrelationId = NormalizeCorrelation(correlationId), CreatedAt = DateTime.UtcNow,
            CreatedBy = UserName, CreatedById = UserId
        };
        var input = request.Lines.ToDictionary(value => value.ProjectBoqVersionLineId);
        foreach (var line in lines.OrderBy(value => value.SortOrder))
        {
            var item = input[line.Id];
            try { QuantitySurveyDesignRevisionImpactRules.ValidateLine(request.Route, item.ImpactType, line.Quantity, item.IndicativeQuantity); }
            catch (ArgumentException exception) { throw Validation($"{line.LineNumber ?? line.ItemCode ?? "BoQ line"}: {exception.Message}"); }
            entity.Lines.Add(new QuantitySurveyDesignRevisionImpactLine
            {
                Id = Guid.NewGuid(), TenantId = TenantId, ImpactId = entity.Id,
                ProjectBoqVersionId = line.ProjectBoqVersionId, ProjectBoqVersionLineId = line.Id,
                BoqLineKey = line.LineKey, ImpactType = item.ImpactType,
                LineReferenceSnapshot = line.LineNumber ?? line.ItemCode ?? line.LineKey.ToString("N")[..12],
                DescriptionSnapshot = line.Description, UnitSnapshot = line.UnitOfMeasure,
                PreviousQuantity = line.Quantity, IndicativeQuantity = item.IndicativeQuantity,
                ImpactReason = RequiredText(item.ImpactReason, 5, 1000, "Line impact reason"),
                CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId
            });
        }
        db.QuantitySurveyDesignRevisionImpacts.Add(entity);
        AddRevision(entity, QuantitySurveyAuditEventMap.CreateDesignRevisionImpact, summary, null, Snapshot(entity), correlationId);
        AddAudit(entity, QuantitySurveyAuditEventMap.CreateDesignRevisionImpact, null, Snapshot(entity), correlationId);
        await SaveAsync(token);
        return await GetAsync(entity.Id, token);
    }

    public async Task<QuantitySurveyDesignImpactDto> SubmitAsync(
        Guid id, QuantitySurveyDesignImpactLifecycleRequest request, string correlationId, CancellationToken token = default)
    {
        var entity = await RequiredAsync(id, true, token);
        await RequireProjectAsync(entity.ProjectId);
        var reason = RequiredText(request.Reason, 5, 2000, "Submission reason");
        var mutationHash = Hash(new { Action = "Submit", Reason = reason });
        if (IsMutationRetry(entity, request.ClientRequestId, mutationHash)) return Map(entity);
        CheckVersion(entity.RowVersion, request.RowVersion);
        if (entity.Status != QuantitySurveyDesignImpactStatuses.Draft) throw Conflict("Only a Draft design impact can be submitted.");
        await ValidateFrozenLineageAsync(entity, token);
        var before = Snapshot(entity);
        var result = await workflow.SubmitAsync(entity.WorkflowEntityTypeCode, entity.Id, entity.ApprovalWorkflowDefinitionId);
        if (!result.ExecutionResult.Success)
            throw Conflict(result.ExecutionResult.Message ?? "The configured design-impact workflow could not be started.");
        workflowAdapters.GetAdapter(entity.WorkflowEntityTypeCode).ApplySubmitOutcome(entity, result.Outcome, UserId);
        entity.Status = QuantitySurveyDesignImpactStatuses.PendingApproval;
        entity.ApprovalStatus = "Pending"; entity.ApprovedById = null; entity.ApprovedAt = null;
        entity.WorkflowInstanceId = result.ExecutionResult.WorkflowInstanceId;
        entity.SubmittedById = UserId; entity.SubmittedAt = DateTime.UtcNow;
        Touch(entity, QuantitySurveyAuditEventMap.SubmitDesignRevisionImpact, correlationId, request.ClientRequestId, mutationHash);
        AddRevision(entity, QuantitySurveyAuditEventMap.SubmitDesignRevisionImpact, reason, before, Snapshot(entity), correlationId);
        AddAudit(entity, QuantitySurveyAuditEventMap.SubmitDesignRevisionImpact, before, Snapshot(entity), correlationId);
        await SaveAsync(token); return await GetAsync(id, token);
    }

    public Task<QuantitySurveyDesignImpactDto> ApproveAsync(Guid id, QuantitySurveyDesignImpactLifecycleRequest request,
        string correlationId, CancellationToken token = default) => DecideAsync(id, request, true, correlationId, token);

    public Task<QuantitySurveyDesignImpactDto> RejectAsync(Guid id, QuantitySurveyDesignImpactLifecycleRequest request,
        string correlationId, CancellationToken token = default) => DecideAsync(id, request, false, correlationId, token);

    public async Task<IReadOnlyList<QuantitySurveyDesignImpactRevisionDto>> HistoryAsync(Guid id, CancellationToken token = default)
    {
        var entity = await RequiredAsync(id, false, token); await RequireProjectAsync(entity.ProjectId);
        return await db.QuantitySurveyDesignRevisionImpactRevisions.AsNoTracking()
            .Where(value => value.TenantId == TenantId && value.ImpactId == id && !value.IsDeleted)
            .OrderByDescending(value => value.CreatedAt).Select(value => new QuantitySurveyDesignImpactRevisionDto
            {
                Id = value.Id, Action = value.Action, ActorName = value.ActorName, ActorRoles = value.ActorRoles,
                CorrelationId = value.CorrelationId, Reason = value.Reason, BeforeJson = value.BeforeJson,
                AfterJson = value.AfterJson, CreatedAt = value.CreatedAt
            }).ToListAsync(token);
    }

    private async Task<QuantitySurveyDesignImpactDto> DecideAsync(Guid id, QuantitySurveyDesignImpactLifecycleRequest request,
        bool approve, string correlationId, CancellationToken token)
    {
        var entity = await RequiredAsync(id, true, token); await RequireProjectAsync(entity.ProjectId);
        var reason = RequiredText(request.Reason, 5, 2000, approve ? "Approval reason" : "Rejection reason");
        var action = approve ? QuantitySurveyAuditEventMap.ApproveDesignRevisionImpact : QuantitySurveyAuditEventMap.RejectDesignRevisionImpact;
        var mutationHash = Hash(new { Action = action, Reason = reason });
        if (IsMutationRetry(entity, request.ClientRequestId, mutationHash)) return Map(entity);
        CheckVersion(entity.RowVersion, request.RowVersion);
        if (entity.Status != QuantitySurveyDesignImpactStatuses.PendingApproval)
            throw Conflict("The design impact must be PendingApproval before a decision.");
        if (entity.SubmittedById == UserId || entity.CreatedByUserId == UserId)
            throw Conflict("Maker-checker control prevents the creator or submitter from deciding this design impact.");
        await ValidateFrozenLineageAsync(entity, token);
        if (!entity.WorkflowInstanceId.HasValue) throw Conflict("The workflow instance is missing.");
        var status = await db.WorkflowInstances.AsNoTracking().Where(value => value.TenantId == TenantId &&
                value.Id == entity.WorkflowInstanceId.Value && !value.IsDeleted)
            .Select(value => (WorkflowInstanceStatus?)value.Status).SingleOrDefaultAsync(token)
            ?? throw Conflict("The shared workflow instance is unavailable.");
        WorkflowOutcome outcome;
        if (approve && status == WorkflowInstanceStatus.Completed) outcome = WorkflowOutcome.Approved;
        else if (!approve && status is WorkflowInstanceStatus.Cancelled or WorkflowInstanceStatus.Failed) outcome = WorkflowOutcome.Rejected;
        else
        {
            if (!approve && status == WorkflowInstanceStatus.Completed)
                throw Conflict("A completed workflow is approved and cannot be rejected.");
            if (approve && status is WorkflowInstanceStatus.Cancelled or WorkflowInstanceStatus.Failed)
                throw Conflict("The workflow ended without approval.");
            if (!await workflow.CanUserApproveAsync(entity.WorkflowEntityTypeCode, entity.Id, UserId))
                throw new UnauthorizedAccessException("You are not assigned to the current design-impact approval step.");
            var result = await workflow.ProcessApprovalAsync(entity.WorkflowEntityTypeCode, entity.Id, UserId,
                approve ? "Approve" : "Reject", reason);
            if (!result.ExecutionResult.Success)
                throw Conflict(result.ExecutionResult.Message ?? "The design-impact workflow decision could not be processed.");
            outcome = result.Outcome;
        }
        if (outcome != (approve ? WorkflowOutcome.Approved : WorkflowOutcome.Rejected))
            throw Conflict("The shared workflow has not reached the requested final outcome.");
        var before = Snapshot(entity);
        entity.Status = approve ? QuantitySurveyDesignImpactStatuses.Approved : QuantitySurveyDesignImpactStatuses.Rejected;
        entity.ApprovalStatus = approve ? "Approved" : "Rejected";
        entity.ApprovedById = approve ? UserId : null; entity.ApprovedAt = approve ? DateTime.UtcNow : null;
        entity.RejectionReason = approve ? null : reason;
        Touch(entity, action, correlationId, request.ClientRequestId, mutationHash);
        AddRevision(entity, action, reason, before, Snapshot(entity), correlationId);
        AddAudit(entity, action, before, Snapshot(entity), correlationId);
        await SaveAsync(token); return await GetAsync(id, token);
    }

    private async Task<Policy> ResolvePolicyAsync(QuantitySurveyDesignImpactRoute route, DateTime asOf, CancellationToken token)
    {
        var profiles = await db.QuantitySurveyConfigurationProfiles.AsNoTracking().Where(value =>
                value.TenantId == TenantId && !value.IsDeleted &&
                value.LifecycleStatus == QuantitySurveyConfigurationProfileStatus.Published && value.PublishedAt != null &&
                value.EffectiveFrom <= asOf && (!value.EffectiveTo.HasValue || value.EffectiveTo.Value >= asOf))
            .OrderByDescending(value => value.IsDefault).ThenByDescending(value => value.Version).ToListAsync(token);
        if (profiles.Count == 0) throw Validation("No Published quantity-survey configuration is effective for this date.");
        if (profiles.Count > 1 && profiles[0].IsDefault == profiles[1].IsDefault)
            throw Conflict("More than one quantity-survey configuration is effective for this date.");
        var profile = profiles[0];
        var key = route == QuantitySurveyDesignImpactRoute.Measurement ? "QS-DEC-007" : "QS-DEC-011";
        var decision = await db.QuantitySurveyConfigurationDecisions.AsNoTracking().SingleOrDefaultAsync(value =>
            value.TenantId == TenantId && value.ProfileId == profile.Id && value.DecisionKey == key && !value.IsDeleted, token)
            ?? throw Validation($"The effective configuration has no {key} decision.");
        if (decision.Status != QuantitySurveyConfigurationDecisionStatus.Approved ||
            decision.ApprovalStatus != QuantitySurveyConfigurationApprovalStatus.Approved ||
            decision.EvidenceStatus != QuantitySurveyConfigurationEvidenceStatus.Verified ||
            (decision.EffectiveFrom.HasValue && decision.EffectiveFrom.Value > asOf) ||
            (decision.EffectiveTo.HasValue && decision.EffectiveTo.Value < asOf))
            throw Validation($"{key} is not approved, verified, and effective for this date.");
        Guid workflowDefinitionId;
        string workflowCode;
        try
        {
            if (route == QuantitySurveyDesignImpactRoute.Measurement)
            {
                workflowDefinitionId = (JsonSerializer.Deserialize<QsMeasurementValue>(decision.ValueJson, JsonOptions)
                    ?? throw new JsonException()).WorkflowDefinitionId;
                workflowCode = QuantitySurveyWorkflowBindingRegistry.Measurement;
            }
            else
            {
                workflowDefinitionId = (JsonSerializer.Deserialize<QsVariationClaimsValue>(decision.ValueJson, JsonOptions)
                    ?? throw new JsonException()).VariationWorkflowDefinitionId;
                workflowCode = QuantitySurveyWorkflowBindingRegistry.Variation;
            }
        }
        catch (JsonException) { throw Conflict($"{key} contains invalid workflow policy data."); }
        if (workflowDefinitionId == Guid.Empty) throw Conflict($"{key} has no configured workflow definition.");
        var validWorkflow = await db.WorkflowDefinitions.AsNoTracking().Include(value => value.EntityType)
            .AnyAsync(value => value.TenantId == TenantId && value.Id == workflowDefinitionId && !value.IsDeleted &&
                               value.IsActive && value.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Published &&
                               !value.EntityType.IsDeleted && value.EntityType.IsActive && value.EntityType.Code == workflowCode, token);
        if (!validWorkflow) throw Validation($"The {key} workflow must be active, Published, and bound to {workflowCode}.");
        return new(profile.Id, decision.Id, workflowDefinitionId, workflowCode,
            Hash(new { Profile = profile.Id, profile.Version, Decision = decision.Id, decision.ValueJson, workflowDefinitionId, workflowCode }));
    }

    private async Task ValidateFrozenLineageAsync(QuantitySurveyDesignRevisionImpact entity, CancellationToken token)
    {
        var drawings = await db.ProjectDrawings.AsNoTracking().Where(value => value.TenantId == TenantId && !value.IsDeleted &&
            (value.Id == entity.PreviousDrawingId || value.Id == entity.RevisedDrawingId)).ToListAsync(token);
        var previous = drawings.SingleOrDefault(value => value.Id == entity.PreviousDrawingId);
        var revised = drawings.SingleOrDefault(value => value.Id == entity.RevisedDrawingId);
        if (previous is null || revised is null) throw Conflict("The governed drawing lineage is no longer available.");
        ValidateDrawingLineage(previous, revised);
        if (entity.PreviousDrawingNumberSnapshot != previous.DrawingNumber || entity.PreviousRevisionSnapshot != previous.Revision ||
            entity.RevisedDrawingNumberSnapshot != revised.DrawingNumber || entity.RevisedRevisionSnapshot != revised.Revision)
            throw Conflict("The drawing revision lineage changed. Create a new design-impact record.");
        var lineIds = entity.Lines.Select(value => value.ProjectBoqVersionLineId).ToList();
        var current = await db.ProjectBoqVersionLines.AsNoTracking().Include(value => value.Version)
            .Where(value => value.TenantId == TenantId && lineIds.Contains(value.Id) && !value.IsDeleted &&
                            value.ProjectId == entity.ProjectId && value.Version.Status == ProjectBoqVersionStatuses.Approved &&
                            value.Version.PublishedAt != null && !value.Version.IsDeleted).ToListAsync(token);
        if (current.Count != lineIds.Count || entity.Lines.Any(line =>
                current.Single(value => value.Id == line.ProjectBoqVersionLineId).LineKey != line.BoqLineKey ||
                current.Single(value => value.Id == line.ProjectBoqVersionLineId).Quantity != line.PreviousQuantity))
            throw Conflict("The approved BoQ lineage changed. Create a new design-impact record against the current BoQ.");
        var policy = await ResolvePolicyAsync(entity.Route, DateTime.UtcNow, token);
        if (policy.ProfileId != entity.ConfigurationProfileId || policy.DecisionId != entity.ConfigurationDecisionId ||
            policy.WorkflowDefinitionId != entity.ApprovalWorkflowDefinitionId || policy.WorkflowEntityTypeCode != entity.WorkflowEntityTypeCode ||
            !FixedEquals(policy.PolicyHash, entity.PolicyHash))
            throw Conflict("The governed QS workflow policy changed. Create a new design-impact record.");
    }

    private static void ValidateDrawingLineage(ProjectDrawing previous, ProjectDrawing revised)
    {
        if (previous.Id == revised.Id || previous.TenantId != revised.TenantId || previous.ProjectId != revised.ProjectId ||
            revised.SupersedesDrawingId != previous.Id ||
            !string.Equals(previous.DrawingNumber, revised.DrawingNumber, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(revised.Revision) ||
            string.Equals(previous.Revision, revised.Revision, StringComparison.OrdinalIgnoreCase))
            throw Validation("The revised drawing must explicitly supersede a different revision of the same drawing in this project.");
        if (revised.Status is not (ProjectDrawingStatuses.ApprovedForConstruction or ProjectDrawingStatuses.ApprovedAsBuilt))
            throw Validation("The revised drawing must be approved for construction or as-built before impact routing.");
        if (previous.Status is not (ProjectDrawingStatuses.Superseded or ProjectDrawingStatuses.ApprovedForConstruction or ProjectDrawingStatuses.ApprovedAsBuilt))
            throw Validation("The prior drawing must be an approved or superseded revision.");
    }

    private async Task RequireProjectAsync(Guid projectId)
    {
        if (projectId == Guid.Empty || await projectService.GetProjectByIdAsync(projectId) is null)
            throw new UnauthorizedAccessException("You are not permitted to access the selected project.");
    }

    private async Task<QuantitySurveyDesignRevisionImpact> RequiredAsync(Guid id, bool tracked, CancellationToken token)
    {
        var query = db.QuantitySurveyDesignRevisionImpacts
            .Include(value => value.Lines).Where(value => value.TenantId == TenantId && value.Id == id && !value.IsDeleted);
        return await (tracked ? query : query.AsNoTracking()).SingleOrDefaultAsync(token)
            ?? throw new QuantitySurveyDesignImpactNotFoundException("The design revision impact was not found.");
    }

    private static QuantitySurveyDesignImpactDto Map(QuantitySurveyDesignRevisionImpact value) => new()
    {
        Id = value.Id, ProjectId = value.ProjectId, PreviousDrawingId = value.PreviousDrawingId,
        RevisedDrawingId = value.RevisedDrawingId,
        PreviousDrawingLabel = value.PreviousDrawingNumberSnapshot + " · Rev " + (value.PreviousRevisionSnapshot ?? "N/A"),
        RevisedDrawingLabel = value.RevisedDrawingNumberSnapshot + " · Rev " + value.RevisedRevisionSnapshot,
        ImpactNumber = value.ImpactNumber, Title = value.Title, ChangeSummary = value.ChangeSummary,
        Route = value.Route, Status = value.Status, ApprovalStatus = value.ApprovalStatus,
        WorkflowInstanceId = value.WorkflowInstanceId, SubmittedAt = value.SubmittedAt,
        ApprovedAt = value.ApprovedAt, RejectionReason = value.RejectionReason,
        RowVersion = Convert.ToBase64String(value.RowVersion),
        Lines = value.Lines.Where(line => !line.IsDeleted).OrderBy(line => line.LineReferenceSnapshot).Select(line => new QuantitySurveyDesignImpactLineDto
        {
            Id = line.Id, ProjectBoqVersionLineId = line.ProjectBoqVersionLineId, BoqLineKey = line.BoqLineKey,
            ImpactType = line.ImpactType, LineReference = line.LineReferenceSnapshot,
            Description = line.DescriptionSnapshot, Unit = line.UnitSnapshot, PreviousQuantity = line.PreviousQuantity,
            IndicativeQuantity = line.IndicativeQuantity, ImpactReason = line.ImpactReason
        }).ToList()
    };

    private void AddRevision(QuantitySurveyDesignRevisionImpact entity, string action, string? reason,
        object? before, object after, string correlationId) => entity.Revisions.Add(new QuantitySurveyDesignRevisionImpactRevision
    {
        Id = Guid.NewGuid(), TenantId = TenantId, ImpactId = entity.Id, Action = action,
        ActorUserId = UserId, ActorName = UserName, ActorRoles = ActorRoles,
        CorrelationId = NormalizeCorrelation(correlationId), Reason = Clean(reason, 2000),
        BeforeJson = before is null ? null : JsonSerializer.Serialize(before, JsonOptions),
        AfterJson = JsonSerializer.Serialize(after, JsonOptions), CreatedAt = DateTime.UtcNow,
        CreatedBy = UserName, CreatedById = UserId
    });

    private void AddAudit(QuantitySurveyDesignRevisionImpact entity, string action, object? before, object after,
        string correlationId) => db.AuditLogs.Add(new AuditLog
    {
        TenantId = TenantId, UserId = UserId, Username = UserName, Action = action,
        Resource = nameof(QuantitySurveyDesignRevisionImpact), ResourceId = entity.Id.ToString(),
        OldValues = before is null ? null : JsonSerializer.Serialize(before, JsonOptions),
        NewValues = JsonSerializer.Serialize(new { correlationId = NormalizeCorrelation(correlationId), value = after }, JsonOptions),
        IpAddress = "api", UserAgent = "QS-0404", Timestamp = DateTime.UtcNow,
        CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId
    });

    private static object Snapshot(QuantitySurveyDesignRevisionImpact value) => new
    {
        value.Id, value.ProjectId, value.PreviousDrawingId, value.RevisedDrawingId,
        value.ImpactNumber, value.Route, value.Status, value.ApprovalStatus,
        value.ConfigurationProfileId, value.ConfigurationDecisionId, value.ApprovalWorkflowDefinitionId,
        value.WorkflowEntityTypeCode, value.WorkflowInstanceId, value.SubmittedById, value.SubmittedAt,
        value.ApprovedById, value.ApprovedAt, value.RejectionReason,
        Lines = value.Lines.Where(line => !line.IsDeleted).Select(line => new
        { line.Id, line.ProjectBoqVersionLineId, line.BoqLineKey, line.ImpactType, line.PreviousQuantity, line.IndicativeQuantity })
    };

    private static bool IsMutationRetry(QuantitySurveyDesignRevisionImpact entity, Guid clientRequestId, string requestHash)
    {
        if (clientRequestId == Guid.Empty) throw Validation("A client request identifier is required.");
        if (entity.LastMutationClientRequestId != clientRequestId) return false;
        if (!FixedEquals(entity.LastMutationRequestHash, requestHash)) throw RetryConflict();
        return true;
    }

    private void Touch(QuantitySurveyDesignRevisionImpact entity, string action, string correlationId,
        Guid clientRequestId, string requestHash)
    {
        entity.AuditAction = action; entity.CorrelationId = NormalizeCorrelation(correlationId);
        entity.LastMutationClientRequestId = clientRequestId; entity.LastMutationRequestHash = requestHash;
        entity.UpdatedAt = DateTime.UtcNow; entity.UpdatedBy = UserName; entity.LastModifiedById = UserId;
    }

    private async Task SaveAsync(CancellationToken token)
    {
        try { await db.SaveChangesAsync(token); }
        catch (DbUpdateConcurrencyException) { throw Conflict("The record changed concurrently. Refresh and retry."); }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException sqlException &&
                                                  sqlException.Number is >= 51131 and <= 51139)
        { throw Conflict(sqlException.Message); }
        catch (DbUpdateException exception) when (exception.InnerException?.Message.Contains("unique", StringComparison.OrdinalIgnoreCase) == true)
        { throw Conflict("This drawing revision and workflow route already has a design-impact record."); }
    }

    private static void CheckVersion(byte[] current, string supplied)
    {
        byte[] expected;
        try { expected = Convert.FromBase64String(supplied); }
        catch (FormatException) { throw Conflict("The row version is invalid. Refresh and retry."); }
        if (!CryptographicOperations.FixedTimeEquals(current, expected)) throw Conflict("The record changed. Refresh and retry.");
    }

    private static string RequiredText(string? value, int min, int max, string label)
    {
        var clean = value?.Trim();
        if (string.IsNullOrWhiteSpace(clean) || clean.Length < min || clean.Length > max)
            throw Validation($"{label} must contain between {min} and {max} characters.");
        return clean;
    }
    private static string? Clean(string? value, int max) => string.IsNullOrWhiteSpace(value) ? null :
        value.Trim().Length <= max ? value.Trim() : throw Validation($"Text cannot exceed {max} characters.");
    private static string NormalizeCorrelation(string? value) => string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") :
        value.Trim().Length <= 100 ? value.Trim() : value.Trim()[..100];
    private static string Hash(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        JsonSerializer.Serialize(value, JsonOptions)))).ToLowerInvariant();
    private static bool FixedEquals(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right)) return false;
        var a = Encoding.UTF8.GetBytes(left); var b = Encoding.UTF8.GetBytes(right);
        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }
    private static QuantitySurveyDesignImpactValidationException Validation(string message) => new(message);
    private static QuantitySurveyDesignImpactConflictException Conflict(string message) => new(message);
    private static QuantitySurveyDesignImpactConflictException RetryConflict() =>
        Conflict("The client request identifier was already used with different content.");

    private sealed record Policy(Guid ProfileId, Guid DecisionId, Guid WorkflowDefinitionId,
        string WorkflowEntityTypeCode, string PolicyHash);
}
