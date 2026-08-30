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
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Services.DocumentManagement;
using ErpSystem.Core.Services.Projects;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Services;

/// <summary>
/// CIV-0203 routing around the existing ProjectRfi owner. This service never owns
/// contractor masters, workflow instances, document bytes or a competing RFI header.
/// </summary>
public sealed class CivilEngineeringRfiService(
    ApplicationDbContext db,
    ICurrentUserService currentUser,
    IProjectService projectService,
    IBusinessPartnerService businessPartnerService,
    IWorkflowIntegrationService workflow) : ICivilEngineeringRfiService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };
    private Guid TenantId => currentUser.TenantId is { } value && value != Guid.Empty ? value : throw new UnauthorizedAccessException("A valid tenant context is required.");
    private Guid UserId => Guid.TryParse(currentUser.UserId, out var value) && value != Guid.Empty ? value : throw new UnauthorizedAccessException("An authenticated user is required.");
    private string UserName => string.IsNullOrWhiteSpace(currentUser.UserName) ? UserId.ToString() : currentUser.UserName.Trim();
    private string ActorRoles => string.Join(',', currentUser.Roles.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value));

    public async Task<CivilEngineeringRfiLookupsDto> GetLookupsAsync(Guid projectId, CancellationToken token = default)
    {
        var project = await RequireInternalProjectAsync(projectId, token);
        var policy = await ResolvePolicyAsync(DateTime.UtcNow, token);
        var engineer = await RequireCurrentEngineerAsync(projectId, policy, token);
        var manager = await RequireCurrentManagerAsync(project, policy, token);
        return new CivilEngineeringRfiLookupsDto
        {
            ProjectEngineerAssignmentId = engineer.Id,
            ProjectEngineerName = await UserNameAsync(engineer.AssignedUserId, token),
            ProjectManagerUserId = manager.Id,
            ProjectManagerName = await UserNameAsync(manager.Id, token),
            RequiresDmsEvidence = policy.Value.RequireDmsEvidence,
            Documents = await DocumentLookupsAsync(token)
        };
    }

    public async Task<IReadOnlyList<CivilEngineeringRfiRoutingDto>> ListAsync(Guid projectId, CancellationToken token = default)
    {
        await RequireInternalProjectAsync(projectId, token);
        var values = await RoutingQuery(false).Where(value => value.ProjectId == projectId)
            .Include(value => value.ProjectRfi).Include(value => value.Evidence).Include(value => value.Responses)
            .OrderByDescending(value => value.CreatedAt).ToListAsync(token);
        return await MapAsync(values, token);
    }

    public async Task<CivilEngineeringRfiRoutingDto> CreateExternalAsync(Guid projectId, CreateCivilEngineeringRfiRequest request, string correlationId, CancellationToken token = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var project = await RequireProjectForExternalRfiAsync(projectId, token);
        var partner = await RequireExternalPartnerAsync(project, null, token);
        var policy = await ResolvePolicyAsync(DateTime.UtcNow, token);
        var errors = CivilEngineeringRfiRoutingPolicy.ValidateCreate(request, policy.Value.RequireDmsEvidence);
        if (errors.Count > 0) throw Validation(errors);
        var requestedEvidence = request.Evidence ?? [];
        var requestHash = Hash(new
        {
            projectId, request.ReferenceNumber, request.Subject, request.Question, request.Priority, request.ProjectPhaseId,
            request.ProjectPackageId, due = request.ResponseDueDate?.ToUniversalTime(),
            evidence = requestedEvidence.OrderBy(value => value.CentralDocumentVersionId).Select(value => new { value.CentralDocumentRecordId, value.CentralDocumentVersionId })
        });
        var retry = await RoutingQuery(true).SingleOrDefaultAsync(value => value.ClientRequestId == request.ClientRequestId, token);
        if (retry is not null)
        {
            if (!FixedEquals(retry.RequestHash, requestHash)) throw Conflict("This client request identifier was already used with different RFI values.");
            await transaction.CommitAsync(token);
            return await GetAsync(retry.Id, token);
        }

        var engineer = await RequireCurrentEngineerAsync(projectId, policy, token);
        var manager = await RequireCurrentManagerAsync(project, policy, token);
        await RequirePhaseAndPackageAsync(projectId, request.ProjectPhaseId, request.ProjectPackageId, token);
        var evidence = await RequireEvidenceAsync(requestedEvidence, policy.Value.RequireDmsEvidence, token);
        var now = DateTime.UtcNow;
        var header = new ProjectRfi
        {
            Id = Guid.NewGuid(), TenantId = TenantId, ProjectId = projectId, ProjectPhaseId = request.ProjectPhaseId,
            ProjectPackageId = request.ProjectPackageId, ReferenceNumber = RequiredText(request.ReferenceNumber, 100, "RFI reference number"),
            Subject = RequiredText(request.Subject, 200, "RFI subject"), Question = RequiredText(request.Question, 4000, "RFI question"),
            Priority = request.Priority.Trim(), Status = ProjectRfiStatuses.Submitted, RaisedDate = now,
            ResponseDueDate = request.ResponseDueDate?.ToUniversalTime(), RaisedByName = UserName,
            CreatedAt = now, CreatedBy = UserName, CreatedById = UserId
        };
        var routing = new ProjectCivilRfiRouting
        {
            Id = Guid.NewGuid(), TenantId = TenantId, ProjectId = projectId, ProjectRfiId = header.Id,
            ProjectEngineerAssignmentId = engineer.Id, ProjectManagerUserId = manager.Id, ExternalBusinessPartnerId = partner.Id,
            ConfigurationProfileId = policy.ProfileId, ConfigurationDecisionId = policy.DecisionId, WorkflowDefinitionId = policy.WorkflowDefinitionId,
            PolicyHash = policy.PolicyHash, Status = CivilEngineeringRfiRoutingStatuses.AwaitingProjectEngineerResponse,
            ApprovalStatus = "Draft", ClientRequestId = request.ClientRequestId, RequestHash = requestHash,
            CorrelationId = Correlation(correlationId), CreatedAt = now, CreatedBy = UserName, CreatedById = UserId
        };
        db.ProjectRfis.Add(header);
        db.ProjectCivilRfiRoutings.Add(routing);
        foreach (var document in evidence)
            routing.Evidence.Add(new ProjectCivilRfiEvidence
            {
                Id = Guid.NewGuid(), TenantId = TenantId, RoutingId = routing.Id, CentralDocumentRecordId = document.DocumentRecordId,
                CentralDocumentVersionId = document.Id, EvidenceRole = "Question", LinkedByUserId = UserId, LinkedAt = now,
                CreatedAt = now, CreatedBy = UserName, CreatedById = UserId
            });
        AddRevision(routing, CivilEngineeringAuditEventMap.CreateRfi, null, Snapshot(routing, header), null, correlationId);
        AddAudit(routing, CivilEngineeringAuditEventMap.CreateRfi, null, Snapshot(routing, header), correlationId);
        await SaveAsync(token);
        await transaction.CommitAsync(token);
        return await GetAsync(routing.Id, token);
    }

    public async Task<CivilEngineeringRfiRoutingDto> SubmitProjectEngineerResponseAsync(Guid routingId, SubmitCivilEngineeringRfiResponseRequest request, string correlationId, CancellationToken token = default)
    {
        var errors = CivilEngineeringRfiRoutingPolicy.ValidateProjectEngineerResponse(request);
        if (errors.Count > 0) throw Validation(errors);
        var requestHash = Hash(new { routingId, response = request.ResponseText.Trim(), request.CentralDocumentRecordId, request.CentralDocumentVersionId });
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var routing = await RoutingQuery(true).Include(value => value.ProjectRfi).SingleOrDefaultAsync(value => value.Id == routingId, token)
            ?? throw new CivilEngineeringSupervisionNotFoundException("The governed RFI was not found.");
        await RequireInternalProjectAsync(routing.ProjectId, token);
        var retry = await db.ProjectCivilRfiResponses.SingleOrDefaultAsync(value => value.TenantId == TenantId && value.RoutingId == routingId && value.ClientRequestId == request.ClientRequestId, token);
        if (retry is not null)
        {
            if (!FixedEquals(retry.RequestHash, requestHash)) throw Conflict("This client request identifier was already used with different response values.");
            await transaction.CommitAsync(token);
            return await GetAsync(routingId, token);
        }
        ApplyRowVersion(routing, request.RowVersion);
        if (!CivilEngineeringRfiRoutingPolicy.CanProjectEngineerRespond(routing.Status)) throw Conflict("This RFI is not awaiting a Project Engineer response.");
        var policy = await ResolveFrozenPolicyAsync(routing, token);
        var engineer = await RequireCurrentEngineerAsync(routing.ProjectId, policy, token);
        if (routing.ProjectEngineerAssignmentId != engineer.Id || engineer.AssignedUserId != UserId)
            throw new UnauthorizedAccessException("Only the routed current Project Engineer can submit this RFI response.");
        var document = await RequireEvidenceAsync(request.CentralDocumentRecordId, request.CentralDocumentVersionId, token);
        var before = Snapshot(routing, routing.ProjectRfi);
        var now = DateTime.UtcNow;
        var response = new ProjectCivilRfiResponse
        {
            Id = Guid.NewGuid(), TenantId = TenantId, RoutingId = routing.Id, ClientRequestId = request.ClientRequestId,
            RequestHash = requestHash,
            Sequence = await db.ProjectCivilRfiResponses.Where(value => value.TenantId == TenantId && value.RoutingId == routing.Id)
                .Select(value => (int?)value.Sequence).MaxAsync(token) + 1 ?? 1,
            ResponseText = request.ResponseText.Trim(), RespondedByUserId = UserId, CentralDocumentRecordId = document.DocumentRecordId,
            CentralDocumentVersionId = document.Id, CorrelationId = Correlation(correlationId), CreatedAt = now, CreatedBy = UserName, CreatedById = UserId
        };
        db.ProjectCivilRfiResponses.Add(response);
        var workflowResult = await workflow.SubmitAsync(CivilEngineeringWorkflowBindingRegistry.Rfi, routing.Id, routing.WorkflowDefinitionId);
        if (!workflowResult.ExecutionResult.Success) throw Conflict(workflowResult.ExecutionResult.Message ?? "The configured RFI response workflow could not be started.");
        if (workflowResult.Outcome != WorkflowOutcome.Pending) throw Conflict("The configured RFI workflow must retain an independent Project Manager approval step.");
        routing.WorkflowInstanceId = workflowResult.ExecutionResult.WorkflowInstanceId;
        routing.Status = CivilEngineeringRfiRoutingStatuses.AwaitingProjectManagerApproval;
        routing.ApprovalStatus = "Pending";
        routing.RejectionReason = null;
        routing.LastMutationClientRequestId = request.ClientRequestId;
        routing.LastMutationRequestHash = requestHash;
        routing.CorrelationId = Correlation(correlationId);
        routing.UpdatedAt = now; routing.UpdatedBy = UserName; routing.LastModifiedById = UserId;
        AddRevision(routing, CivilEngineeringAuditEventMap.SubmitRfiResponse, before, Snapshot(routing, routing.ProjectRfi), null, correlationId);
        AddAudit(routing, CivilEngineeringAuditEventMap.SubmitRfiResponse, before, Snapshot(routing, routing.ProjectRfi), correlationId);
        await SaveAsync(token);
        await transaction.CommitAsync(token);
        return await GetAsync(routing.Id, token);
    }

    public async Task<CivilEngineeringRfiRoutingDto> ProcessProjectManagerResponseAsync(Guid routingId, ProcessCivilEngineeringRfiResponseRequest request, string correlationId, CancellationToken token = default)
    {
        if (request.ClientRequestId == Guid.Empty) throw Validation("A client request identifier is required.");
        if (string.IsNullOrWhiteSpace(request.RowVersion)) throw Validation("Refresh the RFI before processing the response.");
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length < 3) throw Validation("Provide a Project Manager decision reason.");
        var requestHash = Hash(new { routingId, request.Approve, reason = request.Reason.Trim() });
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var routing = await RoutingQuery(true).Include(value => value.ProjectRfi).Include(value => value.Responses).SingleOrDefaultAsync(value => value.Id == routingId, token)
            ?? throw new CivilEngineeringSupervisionNotFoundException("The governed RFI was not found.");
        await RequireInternalProjectAsync(routing.ProjectId, token);
        if (routing.LastMutationClientRequestId == request.ClientRequestId)
        {
            if (!FixedEquals(routing.LastMutationRequestHash ?? string.Empty, requestHash)) throw Conflict("This client request identifier was already used with different decision values.");
            await transaction.CommitAsync(token);
            return await GetAsync(routing.Id, token);
        }
        ApplyRowVersion(routing, request.RowVersion);
        if (!CivilEngineeringRfiRoutingPolicy.CanProjectManagerDecide(routing.Status, routing.ApprovalStatus)) throw Conflict("The RFI response is not awaiting Project Manager approval.");
        var policy = await ResolveFrozenPolicyAsync(routing, token);
        var project = await RequireInternalProjectAsync(routing.ProjectId, token);
        var manager = await RequireCurrentManagerAsync(project, policy, token);
        if (routing.ProjectManagerUserId != manager.Id || manager.Id != UserId)
            throw new UnauthorizedAccessException("Only the routed current Project Manager can process this RFI response.");
        if (!routing.WorkflowInstanceId.HasValue) throw Conflict("The configured RFI workflow was not started.");
        if (!await workflow.CanUserApproveAsync(CivilEngineeringWorkflowBindingRegistry.Rfi, routing.Id, UserId))
            throw new UnauthorizedAccessException("You are not assigned to the active Project Manager RFI approval step.");
        var before = Snapshot(routing, routing.ProjectRfi);
        var workflowResult = await workflow.ProcessApprovalAsync(CivilEngineeringWorkflowBindingRegistry.Rfi, routing.Id, UserId,
            request.Approve ? "Approve" : "Reject", request.Reason.Trim());
        if (!workflowResult.ExecutionResult.Success) throw Conflict(workflowResult.ExecutionResult.Message ?? "The Project Manager RFI workflow action could not be processed.");
        if (request.Approve && workflowResult.Outcome != WorkflowOutcome.Approved) throw Conflict("The shared workflow did not return an approved outcome for the Project Manager decision.");
        if (!request.Approve && workflowResult.Outcome != WorkflowOutcome.Rejected) throw Conflict("The shared workflow did not return a rejected outcome for the Project Manager decision.");
        var now = DateTime.UtcNow;
        if (request.Approve)
        {
            var response = routing.Responses.OrderByDescending(value => value.Sequence).FirstOrDefault()
                ?? throw Conflict("A Project Engineer response is required before Project Manager approval.");
            routing.Status = CivilEngineeringRfiRoutingStatuses.Answered;
            routing.ApprovalStatus = "Approved";
            routing.ApprovedById = UserId; routing.ApprovedAt = now; routing.RejectionReason = null;
            routing.ProjectRfi.Status = ProjectRfiStatuses.Answered;
            routing.ProjectRfi.Response = response.ResponseText; routing.ProjectRfi.RespondedDate = now; routing.ProjectRfi.RespondedByName = UserName;
        }
        else
        {
            routing.Status = CivilEngineeringRfiRoutingStatuses.ReturnedToProjectEngineer;
            routing.ApprovalStatus = "Rejected";
            routing.ApprovedById = null; routing.ApprovedAt = null; routing.RejectionReason = request.Reason.Trim();
            routing.WorkflowInstanceId = null;
            routing.ProjectRfi.Status = ProjectRfiStatuses.Submitted;
        }
        routing.LastMutationClientRequestId = request.ClientRequestId; routing.LastMutationRequestHash = requestHash;
        routing.CorrelationId = Correlation(correlationId); routing.UpdatedAt = now; routing.UpdatedBy = UserName; routing.LastModifiedById = UserId;
        routing.ProjectRfi.UpdatedAt = now; routing.ProjectRfi.UpdatedBy = UserName; routing.ProjectRfi.LastModifiedById = UserId;
        var action = request.Approve ? CivilEngineeringAuditEventMap.ApproveRfiResponse : CivilEngineeringAuditEventMap.RejectRfiResponse;
        AddRevision(routing, action, before, Snapshot(routing, routing.ProjectRfi), request.Reason, correlationId);
        AddAudit(routing, action, before, Snapshot(routing, routing.ProjectRfi), correlationId);
        await SaveAsync(token);
        await transaction.CommitAsync(token);
        return await GetAsync(routing.Id, token);
    }

    private IQueryable<ProjectCivilRfiRouting> RoutingQuery(bool tracked) =>
        (tracked ? db.ProjectCivilRfiRoutings : db.ProjectCivilRfiRoutings.AsNoTracking()).Where(value => value.TenantId == TenantId && !value.IsDeleted);

    private async Task<CivilEngineeringRfiRoutingDto> GetAsync(Guid id, CancellationToken token)
    {
        var value = await RoutingQuery(false).Where(item => item.Id == id).Include(item => item.ProjectRfi).Include(item => item.Evidence).Include(item => item.Responses).SingleOrDefaultAsync(token)
            ?? throw new CivilEngineeringSupervisionNotFoundException("The governed RFI was not found.");
        return (await MapAsync([value], token)).Single();
    }

    private async Task<Project> RequireInternalProjectAsync(Guid projectId, CancellationToken token)
    {
        if (projectId == Guid.Empty || await projectService.GetProjectByIdAsync(projectId) is null)
            throw new UnauthorizedAccessException("You are not permitted to access the selected project.");
        return await db.Projects.AsNoTracking().SingleAsync(value => value.TenantId == TenantId && value.Id == projectId && !value.IsDeleted, token);
    }

    private async Task<Project> RequireProjectForExternalRfiAsync(Guid projectId, CancellationToken token)
        => await db.Projects.AsNoTracking().SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == projectId && !value.IsDeleted && value.ExternalPortalAccessEnabled, token)
           ?? throw new UnauthorizedAccessException("The selected project does not allow controlled external RFI collaboration.");

    private async Task<ErpSystem.Core.DTOs.Procurement.BusinessPartnerDetailDto> RequireExternalPartnerAsync(Project project, ProjectCivilRfiRouting? routing, CancellationToken token)
    {
        var partner = await businessPartnerService.GetByUserIdAsync(UserId) ?? throw new UnauthorizedAccessException("No Business Partner is linked to the current portal user.");
        if (!partner.IsActive || partner.IsBlacklisted || !string.Equals(partner.Status, "Active", StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("The current Business Partner is not active for controlled RFI collaboration.");
        if (routing is not null && partner.Id != routing.ExternalBusinessPartnerId)
            throw new UnauthorizedAccessException("The current portal user is not the controlled contractor or consultant for this RFI.");
        var hasProjectAccess = await db.ProjectExternalAccessPolicies.AsNoTracking().AnyAsync(value =>
            value.TenantId == TenantId && value.ProjectId == project.Id && value.BusinessPartnerId == partner.Id && !value.IsDeleted
            && value.CanComment && value.ArtifactType == "Project", token);
        var hasRfiAccess = routing is not null && await db.ProjectExternalAccessPolicies.AsNoTracking().AnyAsync(value =>
            value.TenantId == TenantId && value.ProjectId == project.Id && value.BusinessPartnerId == partner.Id && !value.IsDeleted
            && value.CanComment && value.ArtifactId == routing.ProjectRfiId, token);
        var allowed = project.BusinessPartnerId == partner.Id || hasProjectAccess || hasRfiAccess;
        if (!allowed) throw new UnauthorizedAccessException("The contractor or consultant has no external collaboration access to this project RFI.");
        return partner;
    }

    private async Task<Policy> ResolveFrozenPolicyAsync(ProjectCivilRfiRouting routing, CancellationToken token)
    {
        var profile = await db.CivilEngineeringConfigurationProfiles.AsNoTracking().SingleOrDefaultAsync(value =>
            value.TenantId == TenantId && value.Id == routing.ConfigurationProfileId && !value.IsDeleted, token)
            ?? throw Conflict("The frozen Civil RFI configuration profile is no longer available.");
        var decision = await db.CivilEngineeringConfigurationDecisions.AsNoTracking().SingleOrDefaultAsync(value =>
            value.TenantId == TenantId && value.Id == routing.ConfigurationDecisionId && value.ProfileId == profile.Id
            && value.ConfigurationKey == "CIV-CFG-005" && !value.IsDeleted, token)
            ?? throw Conflict("The frozen Civil RFI supervision decision is no longer available.");
        if (decision.Status != CivilEngineeringConfigurationDecisionStatus.Approved || decision.ApprovalStatus != CivilEngineeringConfigurationApprovalStatus.Approved || decision.EvidenceStatus != CivilEngineeringConfigurationEvidenceStatus.Verified)
            throw Conflict("The frozen Civil RFI supervision decision is no longer approved and verified.");
        CivilEngineeringSupervisionWorkflowValue value;
        try { value = JsonSerializer.Deserialize<CivilEngineeringSupervisionWorkflowValue>(decision.ValueJson, JsonOptions) ?? throw new JsonException(); }
        catch (JsonException) { throw Conflict("The frozen CIV-CFG-005 supervision decision is invalid."); }
        if (value.RfiWorkflowDefinitionId != routing.WorkflowDefinitionId || !FixedEquals(routing.PolicyHash, Hash(decision.ValueJson)))
            throw Conflict("The frozen Civil RFI workflow lineage no longer matches its approved supervision decision.");
        return new Policy(profile.Id, decision.Id, routing.WorkflowDefinitionId, value, routing.PolicyHash);
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
        if (value.RfiWorkflowDefinitionId == Guid.Empty || value.ProjectEngineerRoleIds.Count == 0 || value.ProjectManagerRoleIds.Count == 0)
            throw Validation("CIV-CFG-005 must select an RFI workflow and Project Engineer and Project Manager roles.");
        var definition = await db.WorkflowDefinitions.AsNoTracking().Include(item => item.EntityType).SingleOrDefaultAsync(item => item.TenantId == TenantId && item.Id == value.RfiWorkflowDefinitionId && !item.IsDeleted && item.IsActive
            && item.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Published && !item.EntityType.IsDeleted && item.EntityType.IsActive && item.EntityType.Code == CivilEngineeringWorkflowBindingRegistry.Rfi, token);
        if (definition is null || !await db.WorkflowSteps.AsNoTracking().AnyAsync(item => item.TenantId == TenantId && item.WorkflowDefinitionId == value.RfiWorkflowDefinitionId && !item.IsDeleted, token))
            throw Validation($"The CIV-CFG-005 RFI workflow must be active, Published, contain a routing step, and be bound to {CivilEngineeringWorkflowBindingRegistry.Rfi}.");
        return new Policy(profile.Id, decision.Id, value.RfiWorkflowDefinitionId, value, Hash(decision.ValueJson));
    }

    private async Task<ProjectCivilProjectEngineerAssignment> RequireCurrentEngineerAsync(Guid projectId, Policy policy, CancellationToken token)
    {
        var values = await db.ProjectCivilProjectEngineerAssignments.AsNoTracking().Where(item => item.TenantId == TenantId && item.ProjectId == projectId && item.IsActive && !item.IsDeleted).Take(2).ToListAsync(token);
        if (values.Count != 1) throw Validation("Exactly one active Project Engineer appointment is required for controlled RFI routing.");
        var value = values[0];
        if (value.Authority == CivilEngineeringProjectEngineerAuthority.SiteSupervision) throw Validation("The active Project Engineer appointment does not include RFI authority.");
        if (value.ConfigurationProfileId != policy.ProfileId || value.ConfigurationDecisionId != policy.DecisionId || !FixedEquals(value.PolicyHash, policy.PolicyHash))
            throw Conflict("The Project Engineer appointment was made under a different supervision policy. Reappoint under the effective policy.");
        return value;
    }

    private async Task<ApplicationUser> RequireCurrentManagerAsync(Project project, Policy policy, CancellationToken token)
    {
        if (!project.ProjectManagerId.HasValue) throw Validation("Assign a Project Manager before routing a Civil RFI.");
        var member = await db.ProjectMembers.AsNoTracking().SingleOrDefaultAsync(item => item.TenantId == TenantId && item.ProjectId == project.Id && item.UserId == project.ProjectManagerId && item.IsActive && !item.IsDeleted, token)
            ?? throw Validation("The selected Project Manager is not an active project member.");
        var roleIds = await db.UserRoles.AsNoTracking().Where(item => item.UserId == member.UserId).Select(item => item.RoleId).ToListAsync(token);
        if (!roleIds.Any(policy.Value.ProjectManagerRoleIds.Contains)) throw Validation("The selected Project Manager does not hold a role configured by CIV-CFG-005.");
        return await db.Users.AsNoTracking().SingleAsync(item => item.TenantId == TenantId && item.Id == member.UserId && item.IsActive, token);
    }

    private async Task RequirePhaseAndPackageAsync(Guid projectId, Guid? phaseId, Guid? packageId, CancellationToken token)
    {
        if (phaseId.HasValue && !await db.ProjectPhases.AsNoTracking().AnyAsync(value => value.TenantId == TenantId && value.Id == phaseId && value.ProjectId == projectId && !value.IsDeleted, token)) throw Validation("The selected project phase is not part of this project.");
        if (packageId.HasValue && !await db.ProjectPackages.AsNoTracking().AnyAsync(value => value.TenantId == TenantId && value.Id == packageId && value.ProjectId == projectId && !value.IsDeleted, token)) throw Validation("The selected work package is not part of this project.");
    }

    private async Task<IReadOnlyList<CentralDocumentVersion>> RequireEvidenceAsync(IEnumerable<CivilEngineeringRfiEvidenceRequest> values, bool required, CancellationToken token)
    {
        var inputs = (values ?? []).GroupBy(item => item.CentralDocumentVersionId).Select(group => group.First()).ToList();
        if (required && inputs.Count == 0) throw Validation("Select current published central-DMS evidence before submitting the RFI.");
        var result = new List<CentralDocumentVersion>();
        foreach (var value in inputs) result.Add(await RequireEvidenceAsync(value.CentralDocumentRecordId, value.CentralDocumentVersionId, token));
        return result;
    }

    private async Task<CentralDocumentVersion> RequireEvidenceAsync(Guid recordId, Guid versionId, CancellationToken token)
    {
        if (recordId == Guid.Empty || versionId == Guid.Empty) throw Validation("Select both the central-DMS document and its current published version.");
        var document = await db.CentralDocumentVersions.AsNoTracking().Include(value => value.DocumentRecord).Where(CentralDocumentEvidenceRules.CurrentPublished())
            .SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == versionId, token) ?? throw Validation("Select a current Published current-tenant central-DMS document version.");
        if (document.DocumentRecordId != recordId) throw Validation("The selected DMS version does not belong to the selected document.");
        return document;
    }

    private async Task<IReadOnlyList<CivilEngineeringRfiDocumentLookupDto>> DocumentLookupsAsync(CancellationToken token) => await db.CentralDocumentVersions.AsNoTracking().Include(value => value.DocumentRecord).Where(CentralDocumentEvidenceRules.CurrentPublished())
        .Where(value => value.TenantId == TenantId).OrderBy(value => value.DocumentRecord.DocumentReference).Select(value => new CivilEngineeringRfiDocumentLookupDto
        { CentralDocumentRecordId = value.DocumentRecordId, CentralDocumentVersionId = value.Id, DocumentReference = value.DocumentRecord.DocumentReference, Title = value.DocumentRecord.Title, VersionNumber = value.VersionNumber }).Take(250).ToListAsync(token);

    private async Task<IReadOnlyList<CivilEngineeringRfiRoutingDto>> MapAsync(IReadOnlyCollection<ProjectCivilRfiRouting> values, CancellationToken token)
    {
        var assignments = await db.ProjectCivilProjectEngineerAssignments.AsNoTracking().Where(value => value.TenantId == TenantId && values.Select(item => item.ProjectEngineerAssignmentId).Contains(value.Id)).ToDictionaryAsync(value => value.Id, token);
        var userIds = values.Select(value => value.ProjectManagerUserId).Concat(values.Select(value => value.ProjectEngineerAssignmentId).Where(assignments.ContainsKey).Select(value => assignments[value].AssignedUserId)).Concat(values.SelectMany(value => value.Responses.Select(response => response.RespondedByUserId))).Distinct().ToList();
        var users = await db.Users.AsNoTracking().Where(value => value.TenantId == TenantId && userIds.Contains(value.Id)).ToDictionaryAsync(value => value.Id, value => DisplayName(value.FirstName, value.LastName, value.UserName), token);
        var partners = await db.BusinessPartners.AsNoTracking().Where(value => value.TenantId == TenantId && values.Select(item => item.ExternalBusinessPartnerId).Contains(value.Id)).ToDictionaryAsync(value => value.Id, value => value.PartnerName, token);
        var versionIds = values.SelectMany(value => value.Evidence.Select(item => item.CentralDocumentVersionId).Concat(value.Responses.Select(item => item.CentralDocumentVersionId))).Distinct().ToList();
        var versions = await db.CentralDocumentVersions.AsNoTracking().Include(value => value.DocumentRecord).Where(value => value.TenantId == TenantId && versionIds.Contains(value.Id)).ToDictionaryAsync(value => value.Id, token);
        return values.Select(value => new CivilEngineeringRfiRoutingDto
        {
            Id = value.Id, ProjectId = value.ProjectId, ProjectRfiId = value.ProjectRfiId, ReferenceNumber = value.ProjectRfi.ReferenceNumber ?? string.Empty,
            Subject = value.ProjectRfi.Subject, Question = value.ProjectRfi.Question, Priority = value.ProjectRfi.Priority, RaisedDate = value.ProjectRfi.RaisedDate, ResponseDueDate = value.ProjectRfi.ResponseDueDate,
            Status = value.Status, ApprovalStatus = value.ApprovalStatus, ProjectEngineerAssignmentId = value.ProjectEngineerAssignmentId,
            ProjectEngineerName = assignments.TryGetValue(value.ProjectEngineerAssignmentId, out var assignment) && users.TryGetValue(assignment.AssignedUserId, out var engineer) ? engineer : "Unavailable user",
            ProjectManagerUserId = value.ProjectManagerUserId, ProjectManagerName = users.GetValueOrDefault(value.ProjectManagerUserId, "Unavailable user"),
            ExternalBusinessPartnerName = partners.GetValueOrDefault(value.ExternalBusinessPartnerId, "Unavailable business partner"), WorkflowInstanceId = value.WorkflowInstanceId,
            RowVersion = Convert.ToBase64String(value.RowVersion),
            Evidence = value.Evidence.Select(item => new CivilEngineeringRfiEvidenceDto { CentralDocumentRecordId = item.CentralDocumentRecordId, CentralDocumentVersionId = item.CentralDocumentVersionId, EvidenceRole = item.EvidenceRole,
                DocumentReference = versions.TryGetValue(item.CentralDocumentVersionId, out var document) ? document.DocumentRecord.DocumentReference : "Unavailable", DocumentTitle = document?.DocumentRecord.Title ?? "Unavailable", VersionNumber = document?.VersionNumber ?? string.Empty }).ToList(),
            Responses = value.Responses.OrderBy(item => item.Sequence).Select(item => new CivilEngineeringRfiResponseDto { Id = item.Id, Sequence = item.Sequence, ResponseText = item.ResponseText, RespondedByUserId = item.RespondedByUserId, RespondedByName = users.GetValueOrDefault(item.RespondedByUserId, "Unavailable user"), Timestamp = item.CreatedAt, CentralDocumentRecordId = item.CentralDocumentRecordId, CentralDocumentVersionId = item.CentralDocumentVersionId }).ToList()
        }).ToList();
    }

    private async Task<string> UserNameAsync(Guid userId, CancellationToken token)
    {
        var user = await db.Users.AsNoTracking().Where(value => value.TenantId == TenantId && value.Id == userId).Select(value => new { value.FirstName, value.LastName, value.UserName }).SingleOrDefaultAsync(token);
        return user is null ? "Unavailable user" : DisplayName(user.FirstName, user.LastName, user.UserName);
    }

    private void AddRevision(ProjectCivilRfiRouting value, string action, object? before, object after, string? reason, string correlationId)
    {
        CivilEngineeringAuditEventMap.GetRequired(action);
        db.ProjectCivilRfiRevisions.Add(new ProjectCivilRfiRevision { Id = Guid.NewGuid(), TenantId = TenantId, RoutingId = value.Id, Action = action, ActorUserId = UserId, ActorName = UserName, ActorRoles = ActorRoles, CorrelationId = Correlation(correlationId), Reason = Clean(reason, 2000), BeforeJson = before is null ? null : JsonSerializer.Serialize(before, JsonOptions), AfterJson = JsonSerializer.Serialize(after, JsonOptions), CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId });
    }

    private void AddAudit(ProjectCivilRfiRouting value, string action, object? before, object after, string correlationId) => db.AuditLogs.Add(new AuditLog { TenantId = TenantId, UserId = UserId, Username = UserName, Action = action, Resource = nameof(ProjectRfi), ResourceId = value.ProjectRfiId.ToString(), OldValues = before is null ? null : JsonSerializer.Serialize(before, JsonOptions), NewValues = JsonSerializer.Serialize(new { correlationId = Correlation(correlationId), value = after }, JsonOptions), IpAddress = currentUser.IpAddress ?? string.Empty, UserAgent = currentUser.UserAgent, Timestamp = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId });

    private async Task SaveAsync(CancellationToken token)
    {
        try { await db.SaveChangesAsync(token); }
        catch (DbUpdateConcurrencyException) { throw Conflict("The governed RFI changed concurrently. Refresh and retry."); }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException sql && sql.Number is >= 52111 and <= 52116) { throw Conflict(sql.Message); }
        catch (DbUpdateException exception) when (exception.InnerException?.Message.Contains("unique", StringComparison.OrdinalIgnoreCase) == true) { throw Conflict("A duplicate or conflicting RFI request was detected. Refresh and retry."); }
    }

    private static void ApplyRowVersion(ProjectCivilRfiRouting routing, string encoded)
    {
        try
        {
            var expected = Convert.FromBase64String(encoded);
            if (!CryptographicOperations.FixedTimeEquals(expected, routing.RowVersion)) throw Conflict("The governed RFI changed concurrently. Refresh and retry.");
        }
        catch (FormatException) { throw Validation("The RFI concurrency token is invalid. Refresh and retry."); }
    }

    private static object Snapshot(ProjectCivilRfiRouting value, ProjectRfi rfi) => new { RoutingId = value.Id, value.ProjectId, value.ProjectRfiId, rfi.ReferenceNumber, rfi.Subject, rfi.Status, value.ProjectEngineerAssignmentId, value.ProjectManagerUserId, value.ExternalBusinessPartnerId, RoutingStatus = value.Status, value.ApprovalStatus, value.WorkflowInstanceId, value.ConfigurationProfileId, value.ConfigurationDecisionId, value.WorkflowDefinitionId, value.PolicyHash };
    private static string RequiredText(string? value, int max, string label) { var normalized = value?.Trim(); if (string.IsNullOrWhiteSpace(normalized)) throw Validation($"{label} is required."); return normalized.Length <= max ? normalized : throw Validation($"{label} cannot exceed {max} characters."); }
    private static string? Clean(string? value, int max) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().Length <= max ? value.Trim() : throw Validation("Text cannot exceed the permitted length.");
    private static string DisplayName(string? first, string? last, string? fallback) { var value = string.Join(' ', new[] { first, last }.Where(item => !string.IsNullOrWhiteSpace(item))).Trim(); return string.IsNullOrWhiteSpace(value) ? fallback ?? string.Empty : value; }
    private static string Correlation(string? value) => string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value[..Math.Min(100, value.Length)];
    private static string Hash(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, JsonOptions))));
    private static bool FixedEquals(string left, string right) => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(left), Encoding.UTF8.GetBytes(right));
    private static CivilEngineeringSupervisionValidationException Validation(string value) => new(value);
    private static CivilEngineeringSupervisionValidationException Validation(IEnumerable<string> values) => new(string.Join(" ", values));
    private static CivilEngineeringSupervisionConflictException Conflict(string value) => new(value);
    private sealed record Policy(Guid ProfileId, Guid DecisionId, Guid WorkflowDefinitionId, CivilEngineeringSupervisionWorkflowValue Value, string PolicyHash);
}
