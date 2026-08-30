using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Entities.QuantitySurvey;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Services.Projects;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Services;

/// <summary>
/// Civil's small commercial-routing overlay. It never posts or changes QS, Projects/Finance or
/// Procurement records; every transition rereads the controlled records from their owners.
/// </summary>
public sealed class CivilEngineeringMaintenanceCostingHandoffService(
    ApplicationDbContext db,
    ICurrentUserService currentUser,
    IWorkflowIntegrationService workflow,
    IWorkflowStatusAdapterRegistry workflowAdapters) : ICivilEngineeringMaintenanceCostingHandoffService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };
    private Guid TenantId => currentUser.TenantId is { } id && id != Guid.Empty ? id : throw new UnauthorizedAccessException("A valid tenant context is required.");
    private Guid UserId => Guid.TryParse(currentUser.UserId, out var id) && id != Guid.Empty ? id : throw new UnauthorizedAccessException("An authenticated user is required.");
    private string UserName => string.IsNullOrWhiteSpace(currentUser.UserName) ? UserId.ToString() : currentUser.UserName.Trim();
    private string ActorRoles => string.Join(',', currentUser.Roles.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value));

    public async Task<CivilEngineeringMaintenanceCostingHandoffLookupsDto> GetLookupsAsync(CancellationToken token = default)
    {
        var permittedProjectIds = await VisibleProjectIdsAsync(token);
        var assessments = await db.CivilEngineeringMaintenanceAssessments.AsNoTracking()
            .Include(value => value.Intake)
            .Where(value => value.TenantId == TenantId && !value.IsDeleted && value.Stage == CivilEngineeringMaintenanceAssessmentStages.Approved && value.HodUserId == UserId)
            .OrderByDescending(value => value.ApprovedAt).Take(250).ToListAsync(token);
        var projects = permittedProjectIds.Count == 0 ? [] : await db.Projects.AsNoTracking()
            .Where(value => value.TenantId == TenantId && !value.IsDeleted && permittedProjectIds.Contains(value.Id))
            .OrderBy(value => value.ProjectCode).ThenBy(value => value.Title).Take(250).ToListAsync(token);
        var projectIds = projects.Select(value => value.Id).ToList();
        var estimates = projectIds.Count == 0 ? [] : await db.QuantitySurveyEstimateVersions.AsNoTracking()
            .Where(value => value.TenantId == TenantId && !value.IsDeleted && projectIds.Contains(value.ProjectId) && value.Status == QuantitySurveyEstimateStatuses.Approved)
            .OrderByDescending(value => value.ApprovedAt).Take(250).ToListAsync(token);
        var budgets = projectIds.Count == 0 ? [] : await db.ProjectBudgetRevisions.AsNoTracking()
            .Where(value => value.TenantId == TenantId && !value.IsDeleted && projectIds.Contains(value.ProjectId) && value.Status == "Approved" && value.EffectiveDate <= DateTime.UtcNow.Date)
            .OrderByDescending(value => value.EffectiveDate).ThenByDescending(value => value.VersionNumber).Take(250).ToListAsync(token);
        var requisitions = projectIds.Count == 0 ? [] : await db.PurchaseRequisitions.AsNoTracking()
            .Where(value => value.TenantId == TenantId && !value.IsDeleted && value.ProjectId.HasValue && projectIds.Contains(value.ProjectId.Value))
            .OrderByDescending(value => value.RequisitionDate).Take(250).ToListAsync(token);
        var contractProjectLookup = projects.Where(value => value.ContractId.HasValue).ToDictionary(value => value.ContractId!.Value, value => value.Id);
        var contractRows = contractProjectLookup.Count == 0 ? [] : await db.Contracts.AsNoTracking()
            .Where(value => value.TenantId == TenantId && !value.IsDeleted && contractProjectLookup.Keys.Contains(value.Id)).OrderBy(value => value.ContractNumber).Take(250).ToListAsync(token);
        return new CivilEngineeringMaintenanceCostingHandoffLookupsDto
        {
            ApprovedAssessments = assessments.Select(value => Option(value.Id, $"{value.Intake.IntakeNumber} - approved scope", value.Status, value.Intake.ProjectId)).ToList(),
            Projects = projects.Select(value => Option(value.Id, $"{value.ProjectCode} - {value.Title}", value.Status)).ToList(),
            ApprovedEstimates = estimates.Select(value => Option(value.Id, $"{value.Name} v{value.VersionNumber} - {value.CurrencyCodeSnapshot} {value.TotalAmount:N2}", value.Status, value.ProjectId)).ToList(),
            ApprovedProjectBudgets = budgets.Select(value => Option(value.Id, $"{value.RevisionName} v{value.VersionNumber} - {value.ApprovedBudget:N2}", value.Status, value.ProjectId)).ToList(),
            PurchaseRequisitions = requisitions.Select(value => Option(value.Id, $"{value.RequisitionNumber} - {value.Currency} {value.TotalAmount:N2}", value.Status, value.ProjectId)).ToList(),
            Contracts = contractRows.Select(value => Option(value.Id, $"{value.ContractNumber} - {value.ContractTitle}", value.Status, contractProjectLookup.GetValueOrDefault(value.Id))).ToList()
        };
    }

    public async Task<IReadOnlyList<CivilEngineeringMaintenanceCostingHandoffDto>> ListAsync(CancellationToken token = default)
    {
        var projectIds = await VisibleProjectIdsAsync(token);
        if (projectIds.Count == 0) return [];
        var values = await Handoffs(false).Where(value => projectIds.Contains(value.ProjectId)).OrderByDescending(value => value.CreatedAt).Take(500).ToListAsync(token);
        return await MapAsync(values, token);
    }

    public async Task<CivilEngineeringMaintenanceCostingHandoffDto> CreateAsync(CreateCivilEngineeringMaintenanceCostingHandoffRequest request, string correlationId, CancellationToken token = default)
    {
        var errors = CivilEngineeringMaintenanceCostingHandoffPolicy.ValidateCreate(request);
        if (errors.Count > 0) throw Validation(errors);
        var requestHash = Hash(new { request.AssessmentId, request.ProjectId, request.QuantitySurveyEstimateVersionId, request.ProjectBudgetRevisionId, request.PurchaseRequisitionId, request.ContractId });
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var retry = await Handoffs(true).SingleOrDefaultAsync(value => value.ClientRequestId == request.ClientRequestId, token);
        if (retry is not null)
        {
            if (!FixedEquals(retry.RequestHash, requestHash)) throw Conflict("This client request identifier was already used with different costing-handoff values.");
            await transaction.CommitAsync(token);
            return (await MapAsync([retry], token)).Single();
        }
        var assessment = await Assessments(true).Include(value => value.Intake).SingleOrDefaultAsync(value => value.Id == request.AssessmentId, token)
            ?? throw new CivilEngineeringMaintenanceAssessmentNotFoundException("The approved Civil maintenance assessment was not found.");
        if (assessment.Stage != CivilEngineeringMaintenanceAssessmentStages.Approved || assessment.Status != CivilEngineeringMaintenanceAssessmentStatuses.Approved)
            throw Validation("Only an approved Civil maintenance scope can be routed for costing and approval.");
        if (assessment.HodUserId != UserId || !HasRole(CivilEngineeringAccessControlRegistry.HeadRole))
            throw new UnauthorizedAccessException("Only the assigned Head of Civil Engineering can create this costing handoff.");
        if (await Handoffs(true).AnyAsync(value => value.AssessmentId == assessment.Id, token))
            throw Conflict("This approved Civil assessment already has a governed costing handoff.");
        var policy = await ResolveCurrentPolicyAsync(assessment, token);
        var owner = await RequireOwnerLinksAsync(assessment, request.ProjectId, request.QuantitySurveyEstimateVersionId, request.ProjectBudgetRevisionId, request.PurchaseRequisitionId, request.ContractId, token);
        var now = DateTime.UtcNow;
        var handoff = new CivilEngineeringMaintenanceCostingHandoff
        {
            Id = Guid.NewGuid(), TenantId = TenantId, AssessmentId = assessment.Id, ProjectId = owner.Project.Id, QuantitySurveyEstimateVersionId = owner.Estimate.Id,
            ProjectBudgetRevisionId = owner.ProjectBudget?.Id, PurchaseRequisitionId = owner.Requisition?.Id, ContractId = owner.Contract?.Id,
            ConfigurationProfileId = policy.Profile.Id, ConfigurationDecisionId = policy.Decision.Id, CostingWorkflowDefinitionId = policy.CostingWorkflow.Id,
            ContractorEngagementWorkflowDefinitionId = policy.ContractorWorkflow.Id, PolicyHash = policy.PolicyHash,
            ClientRequestId = request.ClientRequestId, RequestHash = requestHash, CorrelationId = Correlation(correlationId), CreatedAt = now, CreatedBy = UserName, CreatedById = UserId
        };
        db.CivilEngineeringMaintenanceCostingHandoffs.Add(handoff);
        AddRevision(handoff, CivilEngineeringAuditEventMap.HandoffCostingApproval, null, handoff.Stage, null, null, Snapshot(handoff), correlationId);
        AddAudit(handoff, CivilEngineeringAuditEventMap.HandoffCostingApproval, null, Snapshot(handoff), correlationId);
        await SaveAsync(token);
        await transaction.CommitAsync(token);
        return (await MapAsync([handoff], token)).Single();
    }

    public async Task<CivilEngineeringMaintenanceCostingHandoffDto> ActAsync(Guid handoffId, CivilEngineeringMaintenanceCostingHandoffActionRequest request, string correlationId, CancellationToken token = default)
    {
        if (request.ClientRequestId == Guid.Empty) throw Validation("A client request identifier is required for safe retry.");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var handoff = await Handoffs(true).Include(value => value.Assessment).ThenInclude(value => value.Intake).SingleOrDefaultAsync(value => value.Id == handoffId, token)
            ?? throw new CivilEngineeringMaintenanceCostingHandoffNotFoundException("The Civil maintenance costing handoff was not found.");
        ApplyRowVersion(handoff, request.RowVersion);
        var reason = Clean(request.Reason, 2000);
        var mutationHash = Hash(new { handoffId, request.Action, reason });
        if (handoff.LastMutationClientRequestId == request.ClientRequestId)
        {
            if (!FixedEquals(handoff.LastMutationRequestHash ?? string.Empty, mutationHash)) throw Conflict("This client request identifier was already used with different costing-handoff action values.");
            await transaction.CommitAsync(token);
            return (await MapAsync([handoff], token)).Single();
        }
        var policy = await ResolveFrozenPolicyAsync(handoff, handoff.Assessment, token);
        var owner = await RequireOwnerLinksAsync(handoff.Assessment, handoff.ProjectId, handoff.QuantitySurveyEstimateVersionId, handoff.ProjectBudgetRevisionId, handoff.PurchaseRequisitionId, handoff.ContractId, token);
        var before = Snapshot(handoff); var fromStage = handoff.Stage;
        switch (request.Action)
        {
            case CivilEngineeringMaintenanceCostingHandoffAction.SubmitCosting:
                if (handoff.Stage != CivilEngineeringMaintenanceCostingHandoffStages.Draft) throw Conflict("Only a draft Civil costing handoff can be submitted.");
                RequireAssessmentHod(handoff.Assessment);
                EnsureOwnerState(assessment: handoff.Assessment, owner, policy.Value, requireContractAward: false);
                var submit = await workflow.SubmitAsync(policy.CostingEntityType, handoff.Id, handoff.CostingWorkflowDefinitionId);
                if (!submit.ExecutionResult.Success) throw Conflict(submit.ExecutionResult.Message ?? "The configured Civil costing workflow could not be started.");
                workflowAdapters.GetAdapter(policy.CostingEntityType).ApplySubmitOutcome(handoff, submit.Outcome, UserId);
                handoff.WorkflowInstanceId = submit.ExecutionResult.WorkflowInstanceId;
                handoff.Stage = submit.Outcome == WorkflowOutcome.Approved ? CivilEngineeringMaintenanceCostingHandoffStages.ProcurementAndAward : CivilEngineeringMaintenanceCostingHandoffStages.CostingReview;
                break;
            case CivilEngineeringMaintenanceCostingHandoffAction.ApproveCosting:
                if (handoff.Stage != CivilEngineeringMaintenanceCostingHandoffStages.CostingReview) throw Conflict("Only a pending Civil costing review can be approved.");
                await RequireWorkflowApproverAsync(policy.CostingEntityType, handoff.Id, token);
                await RequireConfiguredCostingReviewerOrApproverAsync(policy.Value, token);
                EnsureOwnerState(handoff.Assessment, owner, policy.Value, requireContractAward: false);
                var approval = await workflow.ProcessApprovalAsync(policy.CostingEntityType, handoff.Id, UserId, "Approve", reason ?? "Civil costing review approved.");
                if (!approval.ExecutionResult.Success || approval.Outcome != WorkflowOutcome.Approved) throw Conflict(approval.ExecutionResult.Message ?? "The Civil costing workflow did not return an approved outcome.");
                workflowAdapters.GetAdapter(policy.CostingEntityType).ApplyApprovalOutcome(handoff, approval.Outcome, UserId, reason);
                handoff.Stage = CivilEngineeringMaintenanceCostingHandoffStages.ProcurementAndAward;
                break;
            case CivilEngineeringMaintenanceCostingHandoffAction.RejectCosting:
                if (handoff.Stage != CivilEngineeringMaintenanceCostingHandoffStages.CostingReview) throw Conflict("Only a pending Civil costing review can be rejected.");
                if (reason is null || reason.Length < 5) throw Validation("A rejection reason of at least 5 characters is required.");
                await RequireWorkflowApproverAsync(policy.CostingEntityType, handoff.Id, token);
                await RequireConfiguredCostingReviewerOrApproverAsync(policy.Value, token);
                var rejection = await workflow.ProcessApprovalAsync(policy.CostingEntityType, handoff.Id, UserId, "Reject", reason);
                if (!rejection.ExecutionResult.Success || rejection.Outcome != WorkflowOutcome.Rejected) throw Conflict(rejection.ExecutionResult.Message ?? "The Civil costing workflow did not return a rejected outcome.");
                workflowAdapters.GetAdapter(policy.CostingEntityType).ApplyApprovalOutcome(handoff, rejection.Outcome, UserId, reason);
                handoff.Stage = CivilEngineeringMaintenanceCostingHandoffStages.Rejected;
                break;
            case CivilEngineeringMaintenanceCostingHandoffAction.RefreshAuthoritativeStatus:
                RequireAssessmentHod(handoff.Assessment);
                if (handoff.Stage != CivilEngineeringMaintenanceCostingHandoffStages.ProcurementAndAward) throw Conflict("Authoritative award status can be refreshed only after costing approval.");
                var readiness = Revalidation(handoff.Assessment, owner, policy.Value, requireContractAward: true);
                handoff.LastRevalidatedAt = DateTime.UtcNow;
                handoff.LastRevalidationSummary = readiness.Summary;
                if (readiness.IsReady)
                {
                    handoff.Stage = CivilEngineeringMaintenanceCostingHandoffStages.Awarded;
                    handoff.Status = CivilEngineeringMaintenanceCostingHandoffStatuses.Awarded;
                    handoff.ApprovalStatus = CivilEngineeringMaintenanceCostingHandoffApprovalStatuses.Approved;
                }
                break;
            default: throw Validation("The Civil costing handoff action is not supported.");
        }
        handoff.LastMutationClientRequestId = request.ClientRequestId; handoff.LastMutationRequestHash = mutationHash; handoff.CorrelationId = Correlation(correlationId);
        handoff.UpdatedAt = DateTime.UtcNow; handoff.UpdatedBy = UserName; handoff.LastModifiedById = UserId;
        var action = request.Action == CivilEngineeringMaintenanceCostingHandoffAction.RefreshAuthoritativeStatus ? CivilEngineeringAuditEventMap.UpdateCivilInterfaceStatus : CivilEngineeringAuditEventMap.HandoffCostingApproval;
        AddRevision(handoff, action, fromStage, handoff.Stage, reason, before, Snapshot(handoff), correlationId);
        AddAudit(handoff, action, before, Snapshot(handoff), correlationId);
        await SaveAsync(token);
        await transaction.CommitAsync(token);
        return (await MapAsync([handoff], token)).Single();
    }

    public async Task<IReadOnlyList<CivilEngineeringMaintenanceCostingHandoffRevisionDto>> GetHistoryAsync(Guid handoffId, CancellationToken token = default)
    {
        var projectIds = await VisibleProjectIdsAsync(token);
        if (!await Handoffs(false).AnyAsync(value => value.Id == handoffId && projectIds.Contains(value.ProjectId), token)) throw new CivilEngineeringMaintenanceCostingHandoffNotFoundException("The Civil maintenance costing handoff was not found.");
        return await db.CivilEngineeringMaintenanceCostingHandoffRevisions.AsNoTracking().Where(value => value.TenantId == TenantId && value.HandoffId == handoffId && !value.IsDeleted).OrderBy(value => value.CreatedAt)
            .Select(value => new CivilEngineeringMaintenanceCostingHandoffRevisionDto { Id = value.Id, Action = value.Action, FromStage = value.FromStage, ToStage = value.ToStage, ActorName = value.ActorName, ActorRoles = value.ActorRoles, Reason = value.Reason, CorrelationId = value.CorrelationId, CreatedAt = value.CreatedAt }).ToListAsync(token);
    }

    private IQueryable<CivilEngineeringMaintenanceCostingHandoff> Handoffs(bool tracked) => (tracked ? db.CivilEngineeringMaintenanceCostingHandoffs : db.CivilEngineeringMaintenanceCostingHandoffs.AsNoTracking()).Where(value => value.TenantId == TenantId && !value.IsDeleted);
    private IQueryable<CivilEngineeringMaintenanceAssessment> Assessments(bool tracked) => (tracked ? db.CivilEngineeringMaintenanceAssessments : db.CivilEngineeringMaintenanceAssessments.AsNoTracking()).Where(value => value.TenantId == TenantId && !value.IsDeleted);

    private async Task<List<Guid>> VisibleProjectIdsAsync(CancellationToken token) =>
        await db.ProjectMembers.AsNoTracking().Where(value => value.TenantId == TenantId && value.UserId == UserId && value.IsActive && !value.IsDeleted).Select(value => value.ProjectId).Distinct().ToListAsync(token);

    private void RequireAssessmentHod(CivilEngineeringMaintenanceAssessment assessment)
    {
        if (assessment.HodUserId != UserId || !HasRole(CivilEngineeringAccessControlRegistry.HeadRole))
            throw new UnauthorizedAccessException("Only the assigned Head of Civil Engineering can perform this handoff action.");
    }

    private bool HasRole(string role) => currentUser.Roles.Any(value => string.Equals(value, role, StringComparison.OrdinalIgnoreCase));

    private async Task RequireWorkflowApproverAsync(string entityType, Guid handoffId, CancellationToken token)
    {
        if (!await workflow.CanUserApproveAsync(entityType, handoffId, UserId))
            throw new UnauthorizedAccessException("You are not assigned to the active Civil costing workflow approval step.");
    }

    private async Task RequireConfiguredCostingReviewerOrApproverAsync(CivilEngineeringCostingApprovalValue controls, CancellationToken token)
    {
        var configuredRoleIds = controls.CostReviewerRoleIds.Concat(controls.ApproverRoleIds).Distinct().ToList();
        var configuredRoleNames = await db.Roles.AsNoTracking()
            .Where(value => configuredRoleIds.Contains(value.Id) && value.Name != null)
            .Select(value => value.Name!)
            .ToListAsync(token);
        if (!CivilEngineeringMaintenanceCostingHandoffPolicy.HasConfiguredCostingReviewerOrApproverRole(currentUser.Roles, configuredRoleNames))
            throw new UnauthorizedAccessException("You do not hold a cost-reviewer or approver role configured by CIV-CFG-008.");
    }

    private async Task<OwnerLinks> RequireOwnerLinksAsync(CivilEngineeringMaintenanceAssessment assessment, Guid projectId, Guid estimateId, Guid? projectBudgetId, Guid? requisitionId, Guid? contractId, CancellationToken token)
    {
        var project = await db.Projects.AsNoTracking().SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == projectId && !value.IsDeleted, token)
            ?? throw Validation("Select an active controlled project for the Civil costing handoff.");
        if (assessment.Intake.ProjectId.HasValue && assessment.Intake.ProjectId != project.Id) throw Validation("The selected project must match the Civil assessment's linked project.");
        if (!await db.ProjectMembers.AsNoTracking().AnyAsync(value => value.TenantId == TenantId && value.ProjectId == project.Id && value.UserId == UserId && value.IsActive && !value.IsDeleted, token)) throw new UnauthorizedAccessException("You are not an active member of the selected delivery project.");
        var estimate = await db.QuantitySurveyEstimateVersions.AsNoTracking().SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == estimateId && value.ProjectId == project.Id && !value.IsDeleted, token)
            ?? throw Validation("Select a QS estimate owned by the selected project.");
        var budget = projectBudgetId.HasValue ? await db.ProjectBudgetRevisions.AsNoTracking().SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == projectBudgetId.Value && value.ProjectId == project.Id && !value.IsDeleted, token) ?? throw Validation("The selected project budget revision is not owned by the selected project.") : null;
        var requisition = requisitionId.HasValue ? await db.PurchaseRequisitions.AsNoTracking().SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == requisitionId.Value && value.ProjectId == project.Id && !value.IsDeleted, token) ?? throw Validation("The selected purchase requisition is not owned by the selected project.") : null;
        var contract = contractId.HasValue ? await db.Contracts.AsNoTracking().SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == contractId.Value && !value.IsDeleted, token) ?? throw Validation("The selected contract is unavailable in the current tenant.") : null;
        if (contract is not null && project.ContractId != contract.Id) throw Validation("The selected contract must be the controlled contract linked to the selected project.");
        return new OwnerLinks(project, estimate, budget, requisition, contract);
    }

    private async Task<Policy> ResolveCurrentPolicyAsync(CivilEngineeringMaintenanceAssessment assessment, CancellationToken token)
    {
        var profile = await db.CivilEngineeringConfigurationProfiles.AsNoTracking().Where(value => value.TenantId == TenantId && !value.IsDeleted && value.LifecycleStatus == CivilEngineeringConfigurationProfileStatus.Published && value.EffectiveFrom <= DateTime.UtcNow && (!value.EffectiveTo.HasValue || value.EffectiveTo >= DateTime.UtcNow)).OrderByDescending(value => value.IsDefault).ThenByDescending(value => value.Version).FirstOrDefaultAsync(token)
            ?? throw Validation("No effective published Civil Engineering configuration profile exists.");
        var decision = await db.CivilEngineeringConfigurationDecisions.AsNoTracking().SingleOrDefaultAsync(value => value.TenantId == TenantId && value.ProfileId == profile.Id && value.ConfigurationKey == "CIV-CFG-008" && !value.IsDeleted, token)
            ?? throw Validation("The effective Civil Engineering configuration has no CIV-CFG-008 costing and approval decision.");
        return await ResolvePolicyAsync(profile, decision, assessment, token);
    }

    private async Task<Policy> ResolveFrozenPolicyAsync(CivilEngineeringMaintenanceCostingHandoff handoff, CivilEngineeringMaintenanceAssessment assessment, CancellationToken token)
    {
        var profile = await db.CivilEngineeringConfigurationProfiles.AsNoTracking().SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == handoff.ConfigurationProfileId && !value.IsDeleted, token)
            ?? throw Conflict("The Civil costing handoff's frozen configuration profile is unavailable.");
        var decision = await db.CivilEngineeringConfigurationDecisions.AsNoTracking().SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == handoff.ConfigurationDecisionId && value.ProfileId == profile.Id && value.ConfigurationKey == "CIV-CFG-008" && !value.IsDeleted, token)
            ?? throw Conflict("The Civil costing handoff's frozen CIV-CFG-008 decision is unavailable.");
        var policy = await ResolvePolicyAsync(profile, decision, assessment, token);
        if (!FixedEquals(handoff.PolicyHash, policy.PolicyHash) || handoff.CostingWorkflowDefinitionId != policy.CostingWorkflow.Id || handoff.ContractorEngagementWorkflowDefinitionId != policy.ContractorWorkflow.Id)
            throw Conflict("The Civil costing handoff's frozen policy lineage no longer matches its controlled configuration.");
        return policy;
    }

    private async Task<Policy> ResolvePolicyAsync(CivilEngineeringConfigurationProfile profile, CivilEngineeringConfigurationDecision decision, CivilEngineeringMaintenanceAssessment assessment, CancellationToken token)
    {
        if (decision.Status != CivilEngineeringConfigurationDecisionStatus.Approved || decision.ApprovalStatus != CivilEngineeringConfigurationApprovalStatus.Approved || decision.EvidenceStatus != CivilEngineeringConfigurationEvidenceStatus.Verified)
            throw Validation("CIV-CFG-008 is not approved and verified.");
        CivilEngineeringCostingApprovalValue controls;
        try { controls = JsonSerializer.Deserialize<CivilEngineeringCostingApprovalValue>(decision.ValueJson, JsonOptions) ?? throw new JsonException(); }
        catch (JsonException) { throw Conflict("CIV-CFG-008 contains invalid costing and approval control data."); }
        var costingType = CivilEngineeringMaintenanceCostingHandoffPolicy.CostingWorkflowEntityType(assessment.Intake.WorkClassification);
        var workflowId = costingType == CivilEngineeringWorkflowBindingRegistry.ComplaintCosting ? controls.ComplaintWorkflowDefinitionId : controls.MaintenanceWorkflowDefinitionId;
        var costing = await RequiredWorkflowAsync(workflowId, costingType, token);
        var contractor = await RequiredWorkflowAsync(controls.ContractorWorkflowDefinitionId, CivilEngineeringWorkflowBindingRegistry.ContractorEngagement, token);
        var roleIds = controls.CostReviewerRoleIds.Concat(controls.ApproverRoleIds).Distinct().ToList();
        if (roleIds.Count == 0 || await db.Roles.AsNoTracking().CountAsync(value => roleIds.Contains(value.Id), token) != roleIds.Count)
            throw Validation("CIV-CFG-008 references an unavailable controlled cost-reviewer or approver role.");
        var errors = CivilEngineeringMaintenanceCostingHandoffPolicy.ValidatePolicy(controls, costing, contractor);
        if (errors.Count > 0) throw Validation(errors);
        return new Policy(profile, decision, controls, costingType, costing, contractor, Hash(decision.ValueJson));
    }

    private async Task<WorkflowDefinition> RequiredWorkflowAsync(Guid id, string entityType, CancellationToken token)
    {
        var workflowDefinition = await db.WorkflowDefinitions.AsNoTracking().Include(value => value.EntityType).Include(value => value.Steps).SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == id && !value.IsDeleted && value.IsActive && value.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Published, token)
            ?? throw Validation($"CIV-CFG-008 must reference an active Published {entityType} workflow.");
        if (workflowDefinition.EntityType is null || workflowDefinition.EntityType.IsDeleted || !workflowDefinition.EntityType.IsActive || !string.Equals(workflowDefinition.EntityType.Code, entityType, StringComparison.OrdinalIgnoreCase))
            throw Validation($"CIV-CFG-008 references a workflow not bound to {entityType}.");
        return workflowDefinition;
    }

    private static void EnsureOwnerState(CivilEngineeringMaintenanceAssessment assessment, OwnerLinks owner, CivilEngineeringCostingApprovalValue controls, bool requireContractAward)
    {
        var readiness = Revalidation(assessment, owner, controls, requireContractAward);
        if (!readiness.IsReady) throw Validation(readiness.Errors);
    }

    private static RevalidationResult Revalidation(CivilEngineeringMaintenanceAssessment assessment, OwnerLinks owner, CivilEngineeringCostingApprovalValue controls, bool requireContractAward)
    {
        var hasApprovedEstimate = owner.Estimate.Status == QuantitySurveyEstimateStatuses.Approved;
        var hasApprovedProjectBudget = owner.ProjectBudget?.Status == "Approved" && owner.ProjectBudget.EffectiveDate <= DateTime.UtcNow.Date;
        var hasApprovedRequisition = owner.Requisition?.Status == "Approved";
        var hasApprovedRequisitionBudget = owner.Requisition is { BudgetValidated: true, BudgetId: not null } && hasApprovedRequisition;
        var errors = CivilEngineeringMaintenanceCostingHandoffPolicy.ValidateCurrentOwnerState(owner.Estimate.TotalAmount, controls, hasApprovedEstimate, hasApprovedProjectBudget, hasApprovedRequisitionBudget, hasApprovedRequisition).ToList();
        if (requireContractAward && owner.Contract is null) errors.Add("A controlled project contract is required before the Civil handoff can return an award status.");
        if (requireContractAward && owner.Contract is not null && !string.Equals(owner.Contract.Status, "Active", StringComparison.OrdinalIgnoreCase)) errors.Add("The linked Procurement contract is not active; award status has not been returned by Procurement.");
        var summary = errors.Count == 0 ? "QS estimate, budget control, procurement linkage and active contract are current." : string.Join(" ", errors);
        return new RevalidationResult(errors.Count == 0, errors, summary);
    }

    private async Task<IReadOnlyList<CivilEngineeringMaintenanceCostingHandoffDto>> MapAsync(IReadOnlyCollection<CivilEngineeringMaintenanceCostingHandoff> values, CancellationToken token)
    {
        var assessmentIds = values.Select(value => value.AssessmentId).Distinct().ToList(); var projectIds = values.Select(value => value.ProjectId).Distinct().ToList(); var estimateIds = values.Select(value => value.QuantitySurveyEstimateVersionId).Distinct().ToList();
        var budgetIds = values.Where(value => value.ProjectBudgetRevisionId.HasValue).Select(value => value.ProjectBudgetRevisionId!.Value).Distinct().ToList(); var requisitionIds = values.Where(value => value.PurchaseRequisitionId.HasValue).Select(value => value.PurchaseRequisitionId!.Value).Distinct().ToList(); var contractIds = values.Where(value => value.ContractId.HasValue).Select(value => value.ContractId!.Value).Distinct().ToList();
        var assessments = await db.CivilEngineeringMaintenanceAssessments.AsNoTracking().Include(value => value.Intake).Where(value => value.TenantId == TenantId && assessmentIds.Contains(value.Id)).ToDictionaryAsync(value => value.Id, value => value.Intake.IntakeNumber, token);
        var projects = await db.Projects.AsNoTracking().Where(value => value.TenantId == TenantId && projectIds.Contains(value.Id)).ToDictionaryAsync(value => value.Id, value => $"{value.ProjectCode} - {value.Title}", token);
        var estimates = await db.QuantitySurveyEstimateVersions.AsNoTracking().Where(value => value.TenantId == TenantId && estimateIds.Contains(value.Id)).ToDictionaryAsync(value => value.Id, token);
        var budgets = budgetIds.Count == 0 ? new Dictionary<Guid, ProjectBudgetRevision>() : await db.ProjectBudgetRevisions.AsNoTracking().Where(value => value.TenantId == TenantId && budgetIds.Contains(value.Id)).ToDictionaryAsync(value => value.Id, token);
        var requisitions = requisitionIds.Count == 0 ? new Dictionary<Guid, PurchaseRequisition>() : await db.PurchaseRequisitions.AsNoTracking().Where(value => value.TenantId == TenantId && requisitionIds.Contains(value.Id)).ToDictionaryAsync(value => value.Id, token);
        var contracts = contractIds.Count == 0 ? new Dictionary<Guid, Contract>() : await db.Contracts.AsNoTracking().Where(value => value.TenantId == TenantId && contractIds.Contains(value.Id)).ToDictionaryAsync(value => value.Id, token);
        return values.Select(value =>
        {
            var estimate = estimates[value.QuantitySurveyEstimateVersionId]; budgets.TryGetValue(value.ProjectBudgetRevisionId ?? Guid.Empty, out var budget); requisitions.TryGetValue(value.PurchaseRequisitionId ?? Guid.Empty, out var requisition); contracts.TryGetValue(value.ContractId ?? Guid.Empty, out var contract);
            return new CivilEngineeringMaintenanceCostingHandoffDto { Id = value.Id, AssessmentId = value.AssessmentId, IntakeNumber = assessments.GetValueOrDefault(value.AssessmentId, "Unavailable intake"), ProjectId = value.ProjectId, ProjectLabel = projects.GetValueOrDefault(value.ProjectId, "Unavailable project"), QuantitySurveyEstimateVersionId = value.QuantitySurveyEstimateVersionId, EstimateLabel = $"{estimate.Name} v{estimate.VersionNumber}", EstimateAmount = estimate.TotalAmount, CurrencyCode = estimate.CurrencyCodeSnapshot, ProjectBudgetRevisionId = value.ProjectBudgetRevisionId, ProjectBudgetLabel = budget is null ? null : $"{budget.RevisionName} v{budget.VersionNumber}", PurchaseRequisitionId = value.PurchaseRequisitionId, PurchaseRequisitionLabel = requisition is null ? null : requisition.RequisitionNumber, ContractId = value.ContractId, ContractLabel = contract is null ? null : $"{contract.ContractNumber} - {contract.ContractTitle}", Stage = value.Stage, Status = value.Status, ApprovalStatus = value.ApprovalStatus, WorkflowInstanceId = value.WorkflowInstanceId, LastRevalidatedAt = value.LastRevalidatedAt, LastRevalidationSummary = value.LastRevalidationSummary, RejectionReason = value.RejectionReason, RowVersion = Convert.ToBase64String(value.RowVersion) };
        }).ToList();
    }

    private void AddRevision(CivilEngineeringMaintenanceCostingHandoff handoff, string action, string? fromStage, string toStage, string? reason, object? before, object after, string correlationId)
    {
        CivilEngineeringAuditEventMap.GetRequired(action);
        db.CivilEngineeringMaintenanceCostingHandoffRevisions.Add(new CivilEngineeringMaintenanceCostingHandoffRevision { Id = Guid.NewGuid(), TenantId = TenantId, HandoffId = handoff.Id, Action = action, FromStage = fromStage, ToStage = toStage, ActorUserId = UserId, ActorName = UserName, ActorRoles = ActorRoles, Reason = reason, CorrelationId = Correlation(correlationId), BeforeJson = before is null ? null : JsonSerializer.Serialize(before, JsonOptions), AfterJson = JsonSerializer.Serialize(after, JsonOptions), CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId });
    }
    private void AddAudit(CivilEngineeringMaintenanceCostingHandoff handoff, string action, object? before, object after, string correlationId) => db.AuditLogs.Add(new AuditLog { TenantId = TenantId, UserId = UserId, Username = UserName, Action = action, Resource = nameof(CivilEngineeringMaintenanceCostingHandoff), ResourceId = handoff.Id.ToString(), OldValues = before is null ? null : JsonSerializer.Serialize(before, JsonOptions), NewValues = JsonSerializer.Serialize(new { correlationId = Correlation(correlationId), value = after }, JsonOptions), IpAddress = currentUser.IpAddress ?? string.Empty, UserAgent = currentUser.UserAgent, Timestamp = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId });
    private async Task SaveAsync(CancellationToken token) { try { await db.SaveChangesAsync(token); } catch (DbUpdateConcurrencyException) { throw Conflict("The Civil costing handoff changed concurrently. Refresh and retry."); } catch (DbUpdateException exception) when (exception.InnerException is SqlException sql && sql.Number is >= 52220 and <= 52239) { throw Conflict(sql.Message); } catch (DbUpdateException exception) when (exception.InnerException?.Message.Contains("unique", StringComparison.OrdinalIgnoreCase) == true) { throw Conflict("A duplicate or conflicting Civil costing handoff was detected. Refresh and retry."); } }
    private static void ApplyRowVersion(CivilEngineeringMaintenanceCostingHandoff value, string rowVersion) { try { value.RowVersion = Convert.FromBase64String(rowVersion); } catch (FormatException) { throw Validation("The costing-handoff concurrency version is invalid."); } }
    private static object Snapshot(CivilEngineeringMaintenanceCostingHandoff value) => new { value.Id, value.AssessmentId, value.ProjectId, value.QuantitySurveyEstimateVersionId, value.ProjectBudgetRevisionId, value.PurchaseRequisitionId, value.ContractId, value.ConfigurationProfileId, value.ConfigurationDecisionId, value.CostingWorkflowDefinitionId, value.ContractorEngagementWorkflowDefinitionId, value.WorkflowInstanceId, value.PolicyHash, value.Stage, value.Status, value.ApprovalStatus, value.LastRevalidatedAt, value.LastRevalidationSummary };
    private static CivilEngineeringMaintenanceCostingHandoffLookupOptionDto Option(Guid id, string label, string status, Guid? projectId = null) => new() { Id = id, Label = label, Status = status, ProjectId = projectId };
    private static string? Clean(string? value, int max) { if (string.IsNullOrWhiteSpace(value)) return null; var normalized = value.Trim(); return normalized.Length <= max ? normalized : throw Validation($"Text cannot exceed {max} characters."); }
    private static string Correlation(string? value) => string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value[..Math.Min(100, value.Length)];
    private static string Hash(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, JsonOptions))));
    private static bool FixedEquals(string left, string right) => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(left), Encoding.UTF8.GetBytes(right));
    private static CivilEngineeringMaintenanceCostingHandoffValidationException Validation(string value) => new(value);
    private static CivilEngineeringMaintenanceCostingHandoffValidationException Validation(IEnumerable<string> values) => new(string.Join(" ", values));
    private static CivilEngineeringMaintenanceCostingHandoffConflictException Conflict(string value) => new(value);
    private sealed record OwnerLinks(Project Project, QuantitySurveyEstimateVersion Estimate, ProjectBudgetRevision? ProjectBudget, PurchaseRequisition? Requisition, Contract? Contract);
    private sealed record Policy(CivilEngineeringConfigurationProfile Profile, CivilEngineeringConfigurationDecision Decision, CivilEngineeringCostingApprovalValue Value, string CostingEntityType, WorkflowDefinition CostingWorkflow, WorkflowDefinition ContractorWorkflow, string PolicyHash);
    private sealed record RevalidationResult(bool IsReady, IReadOnlyList<string> Errors, string Summary);
}
