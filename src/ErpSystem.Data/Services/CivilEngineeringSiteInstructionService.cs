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
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Services.DocumentManagement;
using ErpSystem.Core.Services.Projects;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Services;

/// <summary>
/// Civil controls layered over the authoritative Projects site-instruction record.
/// No document bytes, contractor master data, workflow instance, or project access data is owned here.
/// </summary>
public sealed class CivilEngineeringSiteInstructionService(
    ApplicationDbContext db,
    ICurrentUserService currentUser,
    IProjectService projectService,
    IBusinessPartnerService businessPartnerService,
    IWorkflowIntegrationService workflow,
    IWorkflowStatusAdapterRegistry workflowAdapters) : ICivilEngineeringSiteInstructionService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };
    private Guid TenantId => currentUser.TenantId is { } id && id != Guid.Empty ? id : throw new UnauthorizedAccessException("A valid tenant context is required.");
    private Guid UserId => Guid.TryParse(currentUser.UserId, out var id) && id != Guid.Empty ? id : throw new UnauthorizedAccessException("An authenticated user is required.");
    private string UserName => string.IsNullOrWhiteSpace(currentUser.UserName) ? UserId.ToString() : currentUser.UserName.Trim();
    private string ActorRoles => string.Join(',', currentUser.Roles.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value));

    public async Task<CivilEngineeringSiteInstructionLookupsDto> GetLookupsAsync(Guid projectId, CancellationToken token = default)
    {
        var project = await RequireProjectAsync(projectId, token);
        var policy = await ResolvePolicyAsync(DateTime.UtcNow, token);
        var assignment = await RequireCurrentProjectEngineerAsync(projectId, policy, token);
        var manager = await RequireProjectManagerAsync(project, policy, token);
        var contractor = await RequireContractorAsync(project, token);
        var documents = await DocumentLookupsAsync(projectId, token);
        return new CivilEngineeringSiteInstructionLookupsDto
        {
            ProjectEngineerAssignmentId = assignment.Id,
            ProjectEngineerName = await UserNameAsync(assignment.AssignedUserId, token),
            ProjectManagerUserId = manager.Id,
            ProjectManagerName = await UserNameAsync(manager.Id, token),
            ContractorBusinessPartnerId = contractor.Partner.Id,
            ContractorName = contractor.Partner.PartnerName,
            RequiresDmsEvidence = policy.Value.RequireDmsEvidence,
            Documents = documents
        };
    }

    public async Task<IReadOnlyList<CivilEngineeringSiteInstructionRoutingDto>> ListAsync(Guid projectId, CancellationToken token = default)
    {
        await RequireProjectAsync(projectId, token);
        var values = await RoutingQuery(false).Where(value => value.ProjectId == projectId)
            .Include(value => value.ProjectSiteInstruction).Include(value => value.Evidence).Include(value => value.Responses)
            .OrderByDescending(value => value.CreatedAt).ToListAsync(token);
        return await MapAsync(values, token);
    }

    public async Task<CivilEngineeringSiteInstructionRoutingDto> IssueAsync(Guid projectId, CreateCivilEngineeringSiteInstructionRequest request, string correlationId, CancellationToken token = default)
    {
        var errors = CivilEngineeringSiteInstructionRoutingPolicy.ValidateIssue(request.ClientRequestId, request.ProjectEngineerAssignmentId,
            request.Evidence.FirstOrDefault()?.CentralDocumentVersionId ?? Guid.Empty, request.Title, request.Description);
        if (errors.Count > 0) throw Validation(errors);
        var requestHash = Hash(new { projectId, request.ProjectEngineerAssignmentId, request.ReferenceNumber, request.Title, request.Description,
            request.ProjectPhaseId, request.ProjectPackageId, request.SupersedesRoutingId, effective = Utc(request.EffectiveDate), request.EstimatedCostImpact, request.ScheduleImpactDays,
            evidence = request.Evidence.OrderBy(item => item.CentralDocumentVersionId).Select(item => new { item.CentralDocumentRecordId, item.CentralDocumentVersionId }) });

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var retry = await RoutingQuery(true).SingleOrDefaultAsync(value => value.ClientRequestId == request.ClientRequestId, token);
        if (retry is not null)
        {
            if (!FixedEquals(retry.RequestHash, requestHash)) throw Conflict("This client request identifier was already used with different site-instruction details.");
            await transaction.CommitAsync(token);
            return await GetAsync(retry.Id, token);
        }

        var project = await RequireProjectAsync(projectId, token);
        var policy = await ResolvePolicyAsync(DateTime.UtcNow, token);
        var assignment = await RequireCurrentProjectEngineerAsync(projectId, policy, token);
        if (assignment.Id != request.ProjectEngineerAssignmentId) throw Validation("Refresh and select the current active Project Engineer appointment.");
        var manager = await RequireProjectManagerAsync(project, policy, token);
        var contractor = await RequireContractorAsync(project, token);
        var evidence = await RequireEvidenceAsync(request.Evidence, policy.Value.RequireDmsEvidence, projectId, null, token);
        await RequirePhaseAndPackageAsync(projectId, request.ProjectPhaseId, request.ProjectPackageId, token);

        var now = DateTime.UtcNow;
        ProjectCivilSiteInstructionRouting? superseded = null;
        var instructionVersion = 1;
        if (request.SupersedesRoutingId.HasValue)
        {
            superseded = await RoutingQuery(true).Include(value => value.ProjectSiteInstruction)
                .SingleOrDefaultAsync(value => value.Id == request.SupersedesRoutingId.Value, token)
                ?? throw Validation("The selected site-instruction version was not found in this tenant.");
            if (superseded.ProjectId != projectId)
                throw Validation("A successor must remain within the same controlled project.");
            if (superseded.Status is CivilEngineeringSiteInstructionRoutingStatuses.PendingApproval
                or CivilEngineeringSiteInstructionRoutingStatuses.Rejected
                or CivilEngineeringSiteInstructionRoutingStatuses.Closed
                or CivilEngineeringSiteInstructionRoutingStatuses.Superseded)
                throw Conflict("Only an active issued site-instruction version can be superseded.");
            instructionVersion = checked(superseded.InstructionVersion + 1);
        }
        var instruction = new ProjectSiteInstruction
        {
            Id = Guid.NewGuid(), TenantId = TenantId, ProjectId = projectId, ProjectPhaseId = request.ProjectPhaseId, ProjectPackageId = request.ProjectPackageId,
            InstructionType = ProjectSiteInstructionTypes.EngineerInstruction, ReferenceNumber = RequiredText(request.ReferenceNumber, 100, "Reference number"),
            Title = RequiredText(request.Title, 200, "Title"), Description = RequiredText(request.Description, 4000, "Description"),
            Status = ProjectSiteInstructionStatuses.Draft, IssuedDate = now, EffectiveDate = Utc(request.EffectiveDate),
            IssuedByName = UserName, ResponsibleParty = "Project Manager", EstimatedCostImpact = request.EstimatedCostImpact,
            Currency = string.IsNullOrWhiteSpace(project.BaseCurrencyCode) ? "GHS" : project.BaseCurrencyCode, ScheduleImpactDays = request.ScheduleImpactDays,
            CreatedAt = now, CreatedBy = UserName, CreatedById = UserId
        };
        var routing = new ProjectCivilSiteInstructionRouting
        {
            Id = Guid.NewGuid(), TenantId = TenantId, ProjectId = projectId, ProjectSiteInstructionId = instruction.Id,
            ProjectEngineerAssignmentId = assignment.Id, ProjectManagerUserId = manager.Id, ContractorBusinessPartnerId = contractor.Partner.Id,
            ContractId = contractor.Contract?.Id, InstructionVersion = instructionVersion, SupersedesRoutingId = superseded?.Id,
            ConfigurationProfileId = policy.ProfileId, ConfigurationDecisionId = policy.DecisionId, WorkflowDefinitionId = policy.WorkflowDefinitionId,
            PolicyHash = policy.PolicyHash, ClientRequestId = request.ClientRequestId, RequestHash = requestHash, CorrelationId = Correlation(correlationId),
            Status = CivilEngineeringSiteInstructionRoutingStatuses.PendingApproval, ApprovalStatus = "Pending", CreatedAt = now, CreatedBy = UserName, CreatedById = UserId
        };
        db.ProjectSiteInstructions.Add(instruction);
        db.ProjectCivilSiteInstructionRoutings.Add(routing);
        if (superseded is not null)
        {
            var prior = Snapshot(superseded, superseded.ProjectSiteInstruction);
            superseded.Status = CivilEngineeringSiteInstructionRoutingStatuses.Superseded;
            superseded.UpdatedAt = now; superseded.UpdatedBy = UserName; superseded.LastModifiedById = UserId;
            superseded.ProjectSiteInstruction.Status = ProjectSiteInstructionStatuses.Cancelled;
            superseded.ProjectSiteInstruction.ClosedDate = now;
            superseded.ProjectSiteInstruction.UpdatedAt = now; superseded.ProjectSiteInstruction.UpdatedBy = UserName;
            superseded.ProjectSiteInstruction.LastModifiedById = UserId;
            AddRevision(superseded, CivilEngineeringAuditEventMap.SupersedeSiteInstruction, prior, Snapshot(superseded, superseded.ProjectSiteInstruction),
                $"Superseded by instruction version {instructionVersion}.", correlationId);
            AddAudit(superseded, CivilEngineeringAuditEventMap.SupersedeSiteInstruction, prior, Snapshot(superseded, superseded.ProjectSiteInstruction), correlationId);
        }
        foreach (var document in evidence)
            routing.Evidence.Add(new ProjectCivilSiteInstructionEvidence { Id = Guid.NewGuid(), TenantId = TenantId, RoutingId = routing.Id,
                CentralDocumentRecordId = document.DocumentRecordId, CentralDocumentVersionId = document.Id, EvidenceRole = "Instruction", LinkedByUserId = UserId,
                LinkedAt = now, CreatedAt = now, CreatedBy = UserName, CreatedById = UserId });

        var result = await workflow.SubmitAsync(CivilEngineeringWorkflowBindingRegistry.SiteInstruction, routing.Id, policy.WorkflowDefinitionId);
        if (!result.ExecutionResult.Success) throw Conflict(result.ExecutionResult.Message ?? "The configured Project site-instruction workflow could not be started.");
        workflowAdapters.GetAdapter(CivilEngineeringWorkflowBindingRegistry.SiteInstruction).ApplySubmitOutcome(routing, result.Outcome, UserId);
        routing.WorkflowInstanceId = result.ExecutionResult.WorkflowInstanceId;
        ApplyInstructionProjection(routing, instruction);
        AddRevision(routing, CivilEngineeringAuditEventMap.IssueSiteInstruction, null, Snapshot(routing, instruction), null, correlationId);
        AddAudit(routing, CivilEngineeringAuditEventMap.IssueSiteInstruction, null, Snapshot(routing, instruction), correlationId);
        await SaveAsync(token);
        await transaction.CommitAsync(token);
        return await GetAsync(routing.Id, token);
    }

    public async Task<CivilEngineeringSiteInstructionRoutingDto> ProcessProjectManagerRoutingAsync(Guid routingId, ProcessCivilEngineeringSiteInstructionRoutingRequest request, string correlationId, CancellationToken token = default)
    {
        if (request.ClientRequestId == Guid.Empty) throw Validation("A client request identifier is required.");
        if (string.IsNullOrWhiteSpace(request.RowVersion)) throw Validation("Refresh the site instruction before processing it.");
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length < 3) throw Validation("Provide a Project Manager routing reason.");
        var hash = Hash(new { routingId, request.Approve, reason = request.Reason?.Trim() });
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var routing = await RoutingQuery(true).Include(value => value.ProjectSiteInstruction).SingleOrDefaultAsync(value => value.Id == routingId, token)
            ?? throw new CivilEngineeringSupervisionNotFoundException("The governed site instruction was not found.");
        await RequireProjectAsync(routing.ProjectId, token);
        if (routing.LastMutationClientRequestId == request.ClientRequestId)
        {
            if (!FixedEquals(routing.LastMutationRequestHash ?? string.Empty, hash)) throw Conflict("This routing request identifier was already used with different values.");
            await transaction.CommitAsync(token);
            return await GetAsync(routing.Id, token);
        }
        ApplyRowVersion(routing, request.RowVersion);
        if (routing.ProjectManagerUserId != UserId) throw new UnauthorizedAccessException("Only the routed Project Manager can process this site instruction.");
        if (!routing.WorkflowInstanceId.HasValue) throw Conflict("The configured routing workflow was not started.");
        if (!await workflow.CanUserApproveAsync(CivilEngineeringWorkflowBindingRegistry.SiteInstruction, routing.Id, UserId))
            throw new UnauthorizedAccessException("You are not assigned to the active Project Manager routing step.");
        var before = Snapshot(routing, routing.ProjectSiteInstruction);
        var result = await workflow.ProcessApprovalAsync(CivilEngineeringWorkflowBindingRegistry.SiteInstruction, routing.Id, UserId,
            request.Approve ? "Approve" : "Reject", request.Reason.Trim());
        if (!result.ExecutionResult.Success) throw Conflict(result.ExecutionResult.Message ?? "The Project Manager workflow action could not be processed.");
        if (!request.Approve && result.Outcome != WorkflowOutcome.Rejected) throw Conflict("The shared workflow did not return a rejected outcome.");
        if (request.Approve && result.Outcome == WorkflowOutcome.Rejected) throw Conflict("The shared workflow rejected this approval action.");
        workflowAdapters.GetAdapter(CivilEngineeringWorkflowBindingRegistry.SiteInstruction).ApplyApprovalOutcome(routing, result.Outcome, UserId, request.Reason.Trim());
        routing.LastMutationClientRequestId = request.ClientRequestId; routing.LastMutationRequestHash = hash; routing.CorrelationId = Correlation(correlationId);
        routing.UpdatedAt = DateTime.UtcNow; routing.UpdatedBy = UserName; routing.LastModifiedById = UserId;
        ApplyInstructionProjection(routing, routing.ProjectSiteInstruction);
        AddRevision(routing, CivilEngineeringAuditEventMap.IssueSiteInstruction, before, Snapshot(routing, routing.ProjectSiteInstruction), request.Reason, correlationId);
        AddAudit(routing, CivilEngineeringAuditEventMap.IssueSiteInstruction, before, Snapshot(routing, routing.ProjectSiteInstruction), correlationId);
        await SaveAsync(token); await transaction.CommitAsync(token);
        return await GetAsync(routing.Id, token);
    }

    public async Task<CivilEngineeringSiteInstructionRoutingDto> RecordContractorResponseAsync(Guid routingId, RespondToCivilEngineeringSiteInstructionRequest request, string correlationId, CancellationToken token = default)
    {
        if (request.ClientRequestId == Guid.Empty) throw Validation("A client request identifier is required.");
        if (string.IsNullOrWhiteSpace(request.RowVersion)) throw Validation("Refresh the site instruction before recording a contractor response.");
        var requestHash = Hash(new { routingId, request.Action, message = request.Message?.Trim(), request.CentralDocumentRecordId, request.CentralDocumentVersionId });
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var routing = await RoutingQuery(true).Include(value => value.ProjectSiteInstruction).SingleOrDefaultAsync(value => value.Id == routingId, token)
            ?? throw new CivilEngineeringSupervisionNotFoundException("The governed site instruction was not found.");
        await RequireExternalContractorAsync(routing, token);
        await EnsureCurrentCommercialRouteAsync(routing, token);
        var retry = await db.ProjectCivilSiteInstructionResponses.SingleOrDefaultAsync(value => value.TenantId == TenantId && value.RoutingId == routingId && value.ClientRequestId == request.ClientRequestId, token);
        if (retry is not null)
        {
            if (!FixedEquals(retry.RequestHash, requestHash)) throw Conflict("This contractor response request identifier was already used with different values.");
            await transaction.CommitAsync(token); return await GetAsync(routing.Id, token);
        }
        ApplyRowVersion(routing, request.RowVersion);
        if (!CivilEngineeringSiteInstructionRoutingPolicy.CanReceiveContractorResponse(routing.Status, routing.ApprovalStatus))
            throw Conflict("The instruction must be approved and issued before a contractor can acknowledge or respond.");
        var responseEvidence = await RequireOptionalEvidenceAsync(request.CentralDocumentRecordId, request.CentralDocumentVersionId, routing.ProjectId, routing.ProjectSiteInstructionId, token);
        var errors = CivilEngineeringSiteInstructionRoutingPolicy.ValidateContractorAction(request.Action, request.Message, responseEvidence is not null);
        if (errors.Count > 0) throw Validation(errors);
        var now = DateTime.UtcNow;
        var response = new ProjectCivilSiteInstructionResponse { Id = Guid.NewGuid(), TenantId = TenantId, RoutingId = routing.Id, ClientRequestId = request.ClientRequestId,
            Sequence = await db.ProjectCivilSiteInstructionResponses.Where(value => value.TenantId == TenantId && value.RoutingId == routing.Id).Select(value => (int?)value.Sequence).MaxAsync(token) + 1 ?? 1,
            Action = request.Action.Trim(), Message = request.Message.Trim(), RequestHash = requestHash, ActorUserId = UserId, BusinessPartnerId = routing.ContractorBusinessPartnerId,
            CentralDocumentRecordId = responseEvidence?.DocumentRecordId, CentralDocumentVersionId = responseEvidence?.Id, CorrelationId = Correlation(correlationId),
            CreatedAt = now, CreatedBy = UserName, CreatedById = UserId };
        db.ProjectCivilSiteInstructionResponses.Add(response);
        var before = Snapshot(routing, routing.ProjectSiteInstruction);
        if (response.Action == CivilEngineeringSiteInstructionRoutingPolicy.ContractorAcknowledged && routing.Status == CivilEngineeringSiteInstructionRoutingStatuses.AwaitingContractorAcknowledgement)
            routing.Status = CivilEngineeringSiteInstructionRoutingStatuses.AwaitingContractorAcknowledgement;
        if (response.Action == CivilEngineeringSiteInstructionRoutingPolicy.ContractorAcknowledged)
            routing.ProjectSiteInstruction.Status = ProjectSiteInstructionStatuses.Acknowledged;
        if (response.Action == CivilEngineeringSiteInstructionRoutingPolicy.ContractorResponded)
        {
            routing.Status = CivilEngineeringSiteInstructionRoutingStatuses.AwaitingEngineeringReview;
            routing.ProjectSiteInstruction.Status = ProjectSiteInstructionStatuses.InProgress;
        }
        routing.UpdatedAt = now; routing.UpdatedBy = UserName; routing.LastModifiedById = UserId;
        var action = response.Action == CivilEngineeringSiteInstructionRoutingPolicy.ContractorAcknowledged ? CivilEngineeringAuditEventMap.AcknowledgeSiteInstruction : CivilEngineeringAuditEventMap.UpdateSiteInstructionResponse;
        AddRevision(routing, action, before, Snapshot(routing, routing.ProjectSiteInstruction), response.Message, correlationId);
        AddAudit(routing, action, before, Snapshot(routing, routing.ProjectSiteInstruction), correlationId);
        await SaveAsync(token); await transaction.CommitAsync(token);
        return await GetAsync(routing.Id, token);
    }

    public async Task<CivilEngineeringSiteInstructionRoutingDto> ReviewContractorResponseAsync(Guid routingId, ReviewCivilEngineeringSiteInstructionResponseRequest request, string correlationId, CancellationToken token = default)
    {
        if (request.ClientRequestId == Guid.Empty || string.IsNullOrWhiteSpace(request.RowVersion))
            throw Validation("A client request identifier and current row version are required.");
        var mutationHash = Hash(new { routingId, request.Approve, reason = request.Reason?.Trim(), request.CentralDocumentRecordId, request.CentralDocumentVersionId });
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var routing = await RoutingQuery(true).Include(value => value.ProjectSiteInstruction).SingleOrDefaultAsync(value => value.Id == routingId, token)
            ?? throw new CivilEngineeringSupervisionNotFoundException("The governed site instruction was not found.");
        await RequireProjectAsync(routing.ProjectId, token);
        if (routing.LastMutationClientRequestId == request.ClientRequestId)
        {
            if (!FixedEquals(routing.LastMutationRequestHash ?? string.Empty, mutationHash)) throw Conflict("This engineering-review request identifier was already used with different values.");
            await transaction.CommitAsync(token); return await GetAsync(routing.Id, token);
        }
        ApplyRowVersion(routing, request.RowVersion);
        await EnsureCurrentCommercialRouteAsync(routing, token);
        if (!CivilEngineeringSiteInstructionRoutingPolicy.CanEngineeringReview(routing.Status, routing.ApprovalStatus))
            throw Conflict("The instruction is not awaiting engineering review of a contractor response.");
        var policy = await ResolvePolicyAsync(DateTime.UtcNow, token);
        var engineer = await RequireCurrentProjectEngineerAsync(routing.ProjectId, policy, token);
        if (engineer.Id != routing.ProjectEngineerAssignmentId)
            throw Conflict("The routed Project Engineer appointment has changed. Create a successor instruction version under the current appointment.");
        var evidence = await RequireOptionalEvidenceAsync(request.CentralDocumentRecordId, request.CentralDocumentVersionId, routing.ProjectId, routing.ProjectSiteInstructionId, token);
        var errors = CivilEngineeringSiteInstructionRoutingPolicy.ValidateEngineeringReview(request.Approve, request.Reason, evidence is not null);
        if (errors.Count > 0) throw Validation(errors);
        var before = Snapshot(routing, routing.ProjectSiteInstruction);
        var now = DateTime.UtcNow;
        routing.ContractorResponseReviewedById = UserId; routing.ContractorResponseReviewedAt = now;
        routing.ContractorResponseReviewReason = request.Reason.Trim();
        routing.Status = request.Approve ? CivilEngineeringSiteInstructionRoutingStatuses.AwaitingEngineeringFollowUp : CivilEngineeringSiteInstructionRoutingStatuses.AwaitingContractorAcknowledgement;
        routing.LastMutationClientRequestId = request.ClientRequestId; routing.LastMutationRequestHash = mutationHash;
        routing.CorrelationId = Correlation(correlationId); routing.UpdatedAt = now; routing.UpdatedBy = UserName; routing.LastModifiedById = UserId;
        var action = request.Approve ? CivilEngineeringSiteInstructionRoutingPolicy.EngineeringResponseAccepted : CivilEngineeringSiteInstructionRoutingPolicy.EngineeringResponseReturned;
        await AppendHistoryAsync(routing, request.ClientRequestId, mutationHash, action, request.Reason.Trim(), evidence, null, correlationId, token);
        var auditAction = request.Approve ? CivilEngineeringAuditEventMap.ApproveSiteInstructionResponse : CivilEngineeringAuditEventMap.ReturnSiteInstructionResponse;
        AddRevision(routing, auditAction, before, Snapshot(routing, routing.ProjectSiteInstruction), request.Reason, correlationId);
        AddAudit(routing, auditAction, before, Snapshot(routing, routing.ProjectSiteInstruction), correlationId);
        await SaveAsync(token); await transaction.CommitAsync(token);
        return await GetAsync(routing.Id, token);
    }

    public async Task<CivilEngineeringSiteInstructionRoutingDto> RecordEngineeringFollowUpAsync(Guid routingId, FollowUpCivilEngineeringSiteInstructionRequest request, string correlationId, CancellationToken token = default)
    {
        if (request.ClientRequestId == Guid.Empty || string.IsNullOrWhiteSpace(request.RowVersion))
            throw Validation("A client request identifier and current row version are required.");
        var mutationHash = Hash(new { routingId, request.Action, reason = request.Reason?.Trim(), request.CentralDocumentRecordId, request.CentralDocumentVersionId });
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var routing = await RoutingQuery(true).Include(value => value.ProjectSiteInstruction).SingleOrDefaultAsync(value => value.Id == routingId, token)
            ?? throw new CivilEngineeringSupervisionNotFoundException("The governed site instruction was not found.");
        await RequireProjectAsync(routing.ProjectId, token);
        if (routing.LastMutationClientRequestId == request.ClientRequestId)
        {
            if (!FixedEquals(routing.LastMutationRequestHash ?? string.Empty, mutationHash)) throw Conflict("This engineering follow-up request identifier was already used with different values.");
            await transaction.CommitAsync(token); return await GetAsync(routing.Id, token);
        }
        ApplyRowVersion(routing, request.RowVersion);
        await EnsureCurrentCommercialRouteAsync(routing, token);
        if (!CivilEngineeringSiteInstructionRoutingPolicy.CanFollowUp(routing.Status, routing.ApprovalStatus))
            throw Conflict("The instruction is not awaiting an engineering follow-up or closure decision.");
        var policy = await ResolvePolicyAsync(DateTime.UtcNow, token);
        var engineer = await RequireCurrentProjectEngineerAsync(routing.ProjectId, policy, token);
        if (engineer.Id != routing.ProjectEngineerAssignmentId)
            throw Conflict("The routed Project Engineer appointment has changed. Create a successor instruction version under the current appointment.");
        var evidence = await RequireOptionalEvidenceAsync(request.CentralDocumentRecordId, request.CentralDocumentVersionId, routing.ProjectId, routing.ProjectSiteInstructionId, token);
        var errors = CivilEngineeringSiteInstructionRoutingPolicy.ValidateFollowUp(request.Action, request.Reason, evidence is not null);
        if (errors.Count > 0) throw Validation(errors);
        var before = Snapshot(routing, routing.ProjectSiteInstruction);
        var now = DateTime.UtcNow;
        var close = request.Action == CivilEngineeringSiteInstructionRoutingPolicy.InstructionClosed;
        routing.Status = close ? CivilEngineeringSiteInstructionRoutingStatuses.Closed : CivilEngineeringSiteInstructionRoutingStatuses.AwaitingContractorAcknowledgement;
        if (close)
        {
            routing.ClosedById = UserId; routing.ClosedAt = now;
            routing.ProjectSiteInstruction.Status = ProjectSiteInstructionStatuses.Closed; routing.ProjectSiteInstruction.ClosedDate = now;
        }
        routing.LastMutationClientRequestId = request.ClientRequestId; routing.LastMutationRequestHash = mutationHash;
        routing.CorrelationId = Correlation(correlationId); routing.UpdatedAt = now; routing.UpdatedBy = UserName; routing.LastModifiedById = UserId;
        routing.ProjectSiteInstruction.UpdatedAt = now; routing.ProjectSiteInstruction.UpdatedBy = UserName; routing.ProjectSiteInstruction.LastModifiedById = UserId;
        await AppendHistoryAsync(routing, request.ClientRequestId, mutationHash, request.Action, request.Reason.Trim(), evidence, null, correlationId, token);
        var auditAction = close ? CivilEngineeringAuditEventMap.CloseSiteInstruction : CivilEngineeringAuditEventMap.FollowUpSiteInstruction;
        AddRevision(routing, auditAction, before, Snapshot(routing, routing.ProjectSiteInstruction), request.Reason, correlationId);
        AddAudit(routing, auditAction, before, Snapshot(routing, routing.ProjectSiteInstruction), correlationId);
        await SaveAsync(token); await transaction.CommitAsync(token);
        return await GetAsync(routing.Id, token);
    }

    private IQueryable<ProjectCivilSiteInstructionRouting> RoutingQuery(bool tracked) =>
        (tracked ? db.ProjectCivilSiteInstructionRoutings : db.ProjectCivilSiteInstructionRoutings.AsNoTracking()).Where(value => value.TenantId == TenantId && !value.IsDeleted);

    private async Task<CivilEngineeringSiteInstructionRoutingDto> GetAsync(Guid id, CancellationToken token)
    {
        var value = await RoutingQuery(false).Where(item => item.Id == id).Include(item => item.ProjectSiteInstruction).Include(item => item.Evidence).Include(item => item.Responses).SingleOrDefaultAsync(token)
            ?? throw new CivilEngineeringSupervisionNotFoundException("The governed site instruction was not found.");
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
        var decision = await db.CivilEngineeringConfigurationDecisions.AsNoTracking().SingleOrDefaultAsync(value => value.TenantId == TenantId && value.ProfileId == profile.Id && value.ConfigurationKey == "CIV-CFG-005" && !value.IsDeleted, token)
            ?? throw Validation("The effective Civil Engineering configuration has no CIV-CFG-005 supervision decision.");
        if (decision.Status != CivilEngineeringConfigurationDecisionStatus.Approved || decision.ApprovalStatus != CivilEngineeringConfigurationApprovalStatus.Approved || decision.EvidenceStatus != CivilEngineeringConfigurationEvidenceStatus.Verified)
            throw Validation("CIV-CFG-005 is not approved, verified, and effective for this date.");
        CivilEngineeringSupervisionWorkflowValue value;
        try { value = JsonSerializer.Deserialize<CivilEngineeringSupervisionWorkflowValue>(decision.ValueJson, JsonOptions) ?? throw new JsonException(); }
        catch (JsonException) { throw Conflict("CIV-CFG-005 contains invalid supervision routing data."); }
        if (value.SiteInstructionWorkflowDefinitionId == Guid.Empty || value.ProjectEngineerRoleIds.Count == 0 || value.ProjectManagerRoleIds.Count == 0)
            throw Validation("CIV-CFG-005 must select a Site Instruction workflow and Project Engineer and Project Manager roles.");
        var workflowDefinition = await db.WorkflowDefinitions.AsNoTracking().Include(item => item.EntityType).SingleOrDefaultAsync(item => item.TenantId == TenantId && item.Id == value.SiteInstructionWorkflowDefinitionId && !item.IsDeleted && item.IsActive
            && item.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Published && !item.EntityType.IsDeleted && item.EntityType.IsActive && item.EntityType.Code == CivilEngineeringWorkflowBindingRegistry.SiteInstruction, token);
        if (workflowDefinition is null) throw Validation($"The CIV-CFG-005 site-instruction workflow must be active, Published, and bound to {CivilEngineeringWorkflowBindingRegistry.SiteInstruction}.");
        // Keep the frozen-policy fingerprint identical to the Project Engineer appointment service.
        // Both controls are governed by the same CIV-CFG-005 decision value.
        return new Policy(profile.Id, decision.Id, value.SiteInstructionWorkflowDefinitionId, value, Hash(decision.ValueJson));
    }

    private async Task<ProjectCivilProjectEngineerAssignment> RequireCurrentProjectEngineerAsync(Guid projectId, Policy policy, CancellationToken token)
    {
        var value = await db.ProjectCivilProjectEngineerAssignments.AsNoTracking().SingleOrDefaultAsync(item => item.TenantId == TenantId && item.ProjectId == projectId && item.AssignedUserId == UserId && item.IsActive && !item.IsDeleted, token)
            ?? throw new UnauthorizedAccessException("Only the current Project Engineer can issue the governed site instruction.");
        if (value.Authority == CivilEngineeringProjectEngineerAuthority.SiteSupervision) throw new UnauthorizedAccessException("The active Project Engineer appointment does not include site-instruction authority.");
        if (value.ConfigurationProfileId != policy.ProfileId || value.ConfigurationDecisionId != policy.DecisionId || !FixedEquals(value.PolicyHash, policy.PolicyHash))
            throw Conflict("The Project Engineer appointment was made under a different supervision policy. Reappoint under the effective policy.");
        return value;
    }

    private async Task<ApplicationUser> RequireProjectManagerAsync(Project project, Policy policy, CancellationToken token)
    {
        if (!project.ProjectManagerId.HasValue) throw Validation("Assign a Project Manager before issuing a Civil site instruction.");
        var member = await db.ProjectMembers.AsNoTracking().SingleOrDefaultAsync(item => item.TenantId == TenantId && item.ProjectId == project.Id && item.UserId == project.ProjectManagerId && item.IsActive && !item.IsDeleted, token)
            ?? throw Validation("The selected Project Manager is not an active project member.");
        var roleIds = await db.UserRoles.AsNoTracking().Where(item => item.UserId == member.UserId).Select(item => item.RoleId).ToListAsync(token);
        if (!roleIds.Any(policy.Value.ProjectManagerRoleIds.Contains)) throw Validation("The selected Project Manager does not hold a role configured by CIV-CFG-005.");
        return await db.Users.AsNoTracking().SingleAsync(item => item.TenantId == TenantId && item.Id == member.UserId && item.IsActive, token);
    }

    private async Task<ContractorRoute> RequireContractorAsync(Project project, CancellationToken token)
    {
        if (!project.BusinessPartnerId.HasValue) throw Validation("Link the project to its controlled contractor Business Partner before issuing a Civil site instruction.");
        var contractor = await businessPartnerService.GetByIdAsync(project.BusinessPartnerId.Value) ?? throw Validation("The linked contractor Business Partner was not found.");
        if (!contractor.IsActive || contractor.IsBlacklisted || !string.Equals(contractor.Status, "Active", StringComparison.OrdinalIgnoreCase)) throw Validation("The linked contractor Business Partner is not active for site-instruction routing.");
        Contract? contract = null;
        if (project.ContractId.HasValue)
        {
            contract = await db.Contracts.AsNoTracking().SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == project.ContractId.Value && !value.IsDeleted, token)
                ?? throw Validation("The Project's linked Procurement contract is no longer available.");
            if (!string.Equals(contract.Status, "Active", StringComparison.OrdinalIgnoreCase))
                throw Validation("The Project's linked Procurement contract must be Active before routing a site instruction.");
            if (contract.BusinessPartnerId != contractor.Id)
                throw Validation("The Project's linked contractor does not match its active Procurement contract.");
        }
        return new ContractorRoute(contractor, contract);
    }

    private async Task RequireExternalContractorAsync(ProjectCivilSiteInstructionRouting routing, CancellationToken token)
    {
        var partner = await businessPartnerService.GetByUserIdAsync(UserId) ?? throw new UnauthorizedAccessException("No Business Partner is linked to the current portal user.");
        if (partner.Id != routing.ContractorBusinessPartnerId) throw new UnauthorizedAccessException("The current portal user is not the controlled contractor for this site instruction.");
        var allowed = await db.Projects.AsNoTracking().AnyAsync(project => project.TenantId == TenantId && project.Id == routing.ProjectId && project.ExternalPortalAccessEnabled && !project.IsDeleted
            && (project.BusinessPartnerId == partner.Id || db.ProjectExternalAccessPolicies.Any(policy => policy.TenantId == TenantId && policy.ProjectId == project.Id && policy.BusinessPartnerId == partner.Id && !policy.IsDeleted && (policy.ArtifactType == "Project" || policy.ArtifactId == routing.ProjectSiteInstructionId) && policy.CanComment)), token);
        if (!allowed) throw new UnauthorizedAccessException("The contractor has no external collaboration access to this project site instruction.");
    }

    private async Task EnsureCurrentCommercialRouteAsync(ProjectCivilSiteInstructionRouting routing, CancellationToken token)
    {
        var project = await db.Projects.AsNoTracking().SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == routing.ProjectId && !value.IsDeleted, token)
            ?? throw new UnauthorizedAccessException("The governed project is no longer available.");
        var route = await RequireContractorAsync(project, token);
        if (route.Partner.Id != routing.ContractorBusinessPartnerId || route.Contract?.Id != routing.ContractId)
            throw Conflict("The controlled contractor or contract changed. Create a successor instruction version before continuing.");
    }

    private async Task RequirePhaseAndPackageAsync(Guid projectId, Guid? phaseId, Guid? packageId, CancellationToken token)
    {
        if (phaseId.HasValue && !await db.ProjectPhases.AsNoTracking().AnyAsync(value => value.TenantId == TenantId && value.Id == phaseId && value.ProjectId == projectId && !value.IsDeleted, token)) throw Validation("The selected project phase is not part of this project.");
        if (packageId.HasValue && !await db.ProjectPackages.AsNoTracking().AnyAsync(value => value.TenantId == TenantId && value.Id == packageId && value.ProjectId == projectId && !value.IsDeleted, token)) throw Validation("The selected work package is not part of this project.");
    }

    private async Task<IReadOnlyList<CentralDocumentVersion>> RequireEvidenceAsync(IEnumerable<CivilEngineeringSiteInstructionEvidenceRequest> values, bool required, Guid projectId, Guid? siteInstructionId, CancellationToken token)
    {
        var inputs = values.GroupBy(item => item.CentralDocumentVersionId).Select(group => group.First()).ToList();
        if (required && inputs.Count == 0) throw Validation("Select current published central-DMS evidence before issuing the instruction.");
        var result = new List<CentralDocumentVersion>();
        foreach (var item in inputs) result.Add(await RequireEvidenceAsync(item.CentralDocumentRecordId, item.CentralDocumentVersionId, projectId, siteInstructionId, token));
        return result;
    }

    private async Task<CentralDocumentVersion?> RequireOptionalEvidenceAsync(Guid? recordId, Guid? versionId, Guid projectId, Guid? siteInstructionId, CancellationToken token)
    {
        if (!recordId.HasValue && !versionId.HasValue) return null;
        if (!recordId.HasValue || !versionId.HasValue) throw Validation("Select both the central DMS document and its version.");
        return await RequireEvidenceAsync(recordId.Value, versionId.Value, projectId, siteInstructionId, token);
    }

    private async Task<CentralDocumentVersion> RequireEvidenceAsync(Guid recordId, Guid versionId, Guid projectId, Guid? siteInstructionId, CancellationToken token)
    {
        var document = await db.CentralDocumentVersions.AsNoTracking().Include(value => value.DocumentRecord).Where(CentralDocumentEvidenceRules.CurrentPublished())
            .SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == versionId, token) ?? throw Validation("Select a current Published current-tenant central-DMS document version.");
        if (document.DocumentRecordId != recordId) throw Validation("The selected DMS version does not belong to the selected document.");
        var source = document.DocumentRecord.SourceRecordId;
        if (source != projectId && (!siteInstructionId.HasValue || source != siteInstructionId.Value))
            throw Validation("Select central-DMS evidence linked to this project or governed site instruction.");
        return document;
    }

    private async Task<IReadOnlyList<CivilEngineeringSiteInstructionDocumentLookupDto>> DocumentLookupsAsync(Guid projectId, CancellationToken token) => await db.CentralDocumentVersions.AsNoTracking().Include(value => value.DocumentRecord).Where(CentralDocumentEvidenceRules.CurrentPublished())
        .Where(value => value.TenantId == TenantId && value.DocumentRecord.SourceRecordId == projectId).OrderBy(value => value.DocumentRecord.DocumentReference).Select(value => new CivilEngineeringSiteInstructionDocumentLookupDto
        { CentralDocumentRecordId = value.DocumentRecordId, CentralDocumentVersionId = value.Id, DocumentReference = value.DocumentRecord.DocumentReference, Title = value.DocumentRecord.Title, VersionNumber = value.VersionNumber }).Take(250).ToListAsync(token);

    private static void ApplyInstructionProjection(ProjectCivilSiteInstructionRouting routing, ProjectSiteInstruction instruction)
    {
        if (routing.ApprovalStatus == "Approved") { routing.Status = CivilEngineeringSiteInstructionRoutingStatuses.AwaitingContractorAcknowledgement; instruction.Status = ProjectSiteInstructionStatuses.Issued; }
        else if (routing.ApprovalStatus == "Rejected") { routing.Status = CivilEngineeringSiteInstructionRoutingStatuses.Rejected; instruction.Status = ProjectSiteInstructionStatuses.Cancelled; instruction.ClosedDate = DateTime.UtcNow; }
        else { routing.Status = CivilEngineeringSiteInstructionRoutingStatuses.PendingApproval; instruction.Status = ProjectSiteInstructionStatuses.Draft; }
    }

    private async Task AppendHistoryAsync(
        ProjectCivilSiteInstructionRouting routing,
        Guid clientRequestId,
        string requestHash,
        string action,
        string message,
        CentralDocumentVersion? evidence,
        Guid? businessPartnerId,
        string correlationId,
        CancellationToken token)
    {
        db.ProjectCivilSiteInstructionResponses.Add(new ProjectCivilSiteInstructionResponse
        {
            Id = Guid.NewGuid(), TenantId = TenantId, RoutingId = routing.Id, ClientRequestId = clientRequestId,
            RequestHash = requestHash,
            Sequence = await db.ProjectCivilSiteInstructionResponses.Where(value => value.TenantId == TenantId && value.RoutingId == routing.Id)
                .Select(value => (int?)value.Sequence).MaxAsync(token) + 1 ?? 1,
            Action = action, Message = message, ActorUserId = UserId, BusinessPartnerId = businessPartnerId,
            CentralDocumentRecordId = evidence?.DocumentRecordId, CentralDocumentVersionId = evidence?.Id,
            CorrelationId = Correlation(correlationId), CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId
        });
    }

    private async Task<IReadOnlyList<CivilEngineeringSiteInstructionRoutingDto>> MapAsync(IReadOnlyCollection<ProjectCivilSiteInstructionRouting> values, CancellationToken token)
    {
        var userIds = values.SelectMany(value => new[] { value.ProjectEngineerAssignmentId, value.ProjectManagerUserId }.Concat(value.Responses.Select(response => response.ActorUserId))).ToHashSet();
        var engineerIds = values.Select(value => value.ProjectEngineerAssignmentId).Distinct().ToList();
        var assignments = await db.ProjectCivilProjectEngineerAssignments.AsNoTracking().Where(value => value.TenantId == TenantId && engineerIds.Contains(value.Id)).ToDictionaryAsync(value => value.Id, token);
        userIds.UnionWith(assignments.Values.Select(value => value.AssignedUserId));
        var users = await db.Users.AsNoTracking().Where(value => value.TenantId == TenantId && userIds.Contains(value.Id)).ToDictionaryAsync(value => value.Id, value => DisplayName(value.FirstName, value.LastName, value.UserName), token);
        var partnerIds = values.Select(value => value.ContractorBusinessPartnerId).Distinct().ToList();
        var partners = await db.BusinessPartners.AsNoTracking().Where(value => value.TenantId == TenantId && partnerIds.Contains(value.Id)).ToDictionaryAsync(value => value.Id, value => value.PartnerName, token);
        var versionIds = values.SelectMany(value => value.Evidence.Select(item => item.CentralDocumentVersionId).Concat(value.Responses.Where(item => item.CentralDocumentVersionId.HasValue).Select(item => item.CentralDocumentVersionId!.Value))).Distinct().ToList();
        var versions = await db.CentralDocumentVersions.AsNoTracking().Include(value => value.DocumentRecord).Where(value => value.TenantId == TenantId && versionIds.Contains(value.Id)).ToDictionaryAsync(value => value.Id, token);
        return values.Select(value => new CivilEngineeringSiteInstructionRoutingDto
        {
            Id = value.Id, ProjectId = value.ProjectId, ProjectSiteInstructionId = value.ProjectSiteInstructionId,
            ReferenceNumber = value.ProjectSiteInstruction.ReferenceNumber ?? string.Empty, Title = value.ProjectSiteInstruction.Title, Description = value.ProjectSiteInstruction.Description ?? string.Empty,
            Status = value.Status, ApprovalStatus = value.ApprovalStatus, ProjectEngineerAssignmentId = value.ProjectEngineerAssignmentId,
            ProjectEngineerName = assignments.TryGetValue(value.ProjectEngineerAssignmentId, out var assignment) && users.TryGetValue(assignment.AssignedUserId, out var engineer) ? engineer : "Unavailable user",
            ProjectManagerUserId = value.ProjectManagerUserId, ProjectManagerName = users.GetValueOrDefault(value.ProjectManagerUserId, "Unavailable user"),
            ContractorBusinessPartnerId = value.ContractorBusinessPartnerId, ContractorName = partners.GetValueOrDefault(value.ContractorBusinessPartnerId, "Unavailable contractor"),
            ContractId = value.ContractId, InstructionVersion = value.InstructionVersion, SupersedesRoutingId = value.SupersedesRoutingId,
            ContractorResponseReviewedById = value.ContractorResponseReviewedById, ContractorResponseReviewedAt = value.ContractorResponseReviewedAt,
            ContractorResponseReviewReason = value.ContractorResponseReviewReason, ClosedById = value.ClosedById, ClosedAt = value.ClosedAt,
            WorkflowInstanceId = value.WorkflowInstanceId, CreatedAt = value.CreatedAt, RowVersion = Convert.ToBase64String(value.RowVersion),
            Evidence = value.Evidence.Select(item => new CivilEngineeringSiteInstructionEvidenceDto { CentralDocumentRecordId = item.CentralDocumentRecordId, CentralDocumentVersionId = item.CentralDocumentVersionId, EvidenceRole = item.EvidenceRole,
                DocumentReference = versions.TryGetValue(item.CentralDocumentVersionId, out var document) ? document.DocumentRecord.DocumentReference : "Unavailable", DocumentTitle = document?.DocumentRecord.Title ?? "Unavailable", VersionNumber = document?.VersionNumber ?? string.Empty }).ToList(),
            Responses = value.Responses.OrderBy(item => item.Sequence).Select(item => new CivilEngineeringSiteInstructionResponseDto { Id = item.Id, Sequence = item.Sequence, Action = item.Action, Message = item.Message, ActorUserId = item.ActorUserId,
                ActorName = users.GetValueOrDefault(item.ActorUserId, "Unavailable user"), Timestamp = item.CreatedAt, CentralDocumentRecordId = item.CentralDocumentRecordId, CentralDocumentVersionId = item.CentralDocumentVersionId }).ToList()
        }).ToList();
    }

    private async Task<string> UserNameAsync(Guid userId, CancellationToken token)
    {
        var user = await db.Users.AsNoTracking().Where(value => value.TenantId == TenantId && value.Id == userId)
            .Select(value => new { value.FirstName, value.LastName, value.UserName }).SingleOrDefaultAsync(token);
        return user is null ? "Unavailable user" : DisplayName(user.FirstName, user.LastName, user.UserName);
    }
    private void AddRevision(ProjectCivilSiteInstructionRouting value, string action, object? before, object after, string? reason, string correlationId) { CivilEngineeringAuditEventMap.GetRequired(action); db.ProjectCivilSiteInstructionRevisions.Add(new() { Id = Guid.NewGuid(), TenantId = TenantId, RoutingId = value.Id, Action = action, ActorUserId = UserId, ActorName = UserName, ActorRoles = ActorRoles, CorrelationId = Correlation(correlationId), Reason = Clean(reason, 2000), BeforeJson = before is null ? null : JsonSerializer.Serialize(before, JsonOptions), AfterJson = JsonSerializer.Serialize(after, JsonOptions), CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId }); }
    private void AddAudit(ProjectCivilSiteInstructionRouting value, string action, object? before, object after, string correlationId) => db.AuditLogs.Add(new AuditLog { TenantId = TenantId, UserId = UserId, Username = UserName, Action = action, Resource = nameof(ProjectSiteInstruction), ResourceId = value.ProjectSiteInstructionId.ToString(), OldValues = before is null ? null : JsonSerializer.Serialize(before, JsonOptions), NewValues = JsonSerializer.Serialize(new { correlationId = Correlation(correlationId), value = after }, JsonOptions), IpAddress = currentUser.IpAddress ?? string.Empty, UserAgent = currentUser.UserAgent, Timestamp = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId });
    private async Task SaveAsync(CancellationToken token) { try { await db.SaveChangesAsync(token); } catch (DbUpdateConcurrencyException) { throw Conflict("The governed site instruction changed concurrently. Refresh and retry."); } catch (DbUpdateException exception) when (exception.InnerException is SqlException sql && sql.Number is >= 52011 and <= 52077) { throw Conflict(sql.Message); } catch (DbUpdateException exception) when (exception.InnerException?.Message.Contains("unique", StringComparison.OrdinalIgnoreCase) == true) { throw Conflict("A duplicate or conflicting site-instruction request was detected. Refresh and retry."); } }
    private static object Snapshot(ProjectCivilSiteInstructionRouting value, ProjectSiteInstruction instruction) => new { RoutingId = value.Id, value.ProjectId, value.ProjectSiteInstructionId, instruction.ReferenceNumber, instruction.Title, InstructionStatus = instruction.Status, value.ProjectEngineerAssignmentId, value.ProjectManagerUserId, value.ContractorBusinessPartnerId, value.ContractId, value.InstructionVersion, value.SupersedesRoutingId, RoutingStatus = value.Status, value.ApprovalStatus, value.ContractorResponseReviewedById, value.ContractorResponseReviewedAt, value.ContractorResponseReviewReason, value.ClosedById, value.ClosedAt, value.WorkflowInstanceId, value.ConfigurationProfileId, value.ConfigurationDecisionId, value.WorkflowDefinitionId, value.PolicyHash };
    private static string RequiredText(string? value, int max, string label) { var normalized = value?.Trim(); if (string.IsNullOrWhiteSpace(normalized)) throw Validation($"{label} is required."); return normalized.Length <= max ? normalized : throw Validation($"{label} cannot exceed {max} characters."); }
    private static string? Clean(string? value, int max) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().Length <= max ? value.Trim() : throw Validation($"Text cannot exceed {max} characters.");
    private static string DisplayName(string? first, string? last, string? fallback) { var value = string.Join(' ', new[] { first, last }.Where(item => !string.IsNullOrWhiteSpace(item))).Trim(); return string.IsNullOrWhiteSpace(value) ? fallback ?? string.Empty : value; }
    private static string Correlation(string? value) => string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value[..Math.Min(100, value.Length)];
    private static DateTime Utc(DateTime value) => value == default ? DateTime.UtcNow : value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
    private static string Hash(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, JsonOptions))));
    private static bool FixedEquals(string left, string right) => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(left), Encoding.UTF8.GetBytes(right));
    private static void ApplyRowVersion(ProjectCivilSiteInstructionRouting routing, string rowVersion)
    {
        byte[] expected;
        try { expected = Convert.FromBase64String(rowVersion); }
        catch (FormatException) { throw Conflict("The governed site-instruction row version is invalid. Refresh and retry."); }
        if (!CryptographicOperations.FixedTimeEquals(routing.RowVersion, expected))
            throw Conflict("The governed site instruction changed. Refresh and retry.");
    }
    private static CivilEngineeringSupervisionValidationException Validation(string value) => new(value);
    private static CivilEngineeringSupervisionValidationException Validation(IEnumerable<string> values) => new(string.Join(" ", values));
    private static CivilEngineeringSupervisionConflictException Conflict(string value) => new(value);
    private sealed record ContractorRoute(ErpSystem.Core.DTOs.Procurement.BusinessPartnerDetailDto Partner, Contract? Contract);
    private sealed record Policy(Guid ProfileId, Guid DecisionId, Guid WorkflowDefinitionId, CivilEngineeringSupervisionWorkflowValue Value, string PolicyHash);
}
