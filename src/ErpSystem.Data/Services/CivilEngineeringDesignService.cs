using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Data;
using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.Estate;
using ErpSystem.Core.Entities.Finance.FixedAssets;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Services.DocumentManagement;
using ErpSystem.Core.Services.Projects;
using ErpSystem.Core.Services.Workflow;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Services;

public sealed partial class CivilEngineeringDesignService(
    ApplicationDbContext db,
    ICurrentUserService currentUser,
    IProjectService projectService,
    IWorkflowIntegrationService workflow,
    IWorkflowStatusAdapterRegistry workflowAdapters) : ICivilEngineeringDesignService
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
    private string UserName => string.IsNullOrWhiteSpace(currentUser.UserName)
        ? UserId.ToString()
        : currentUser.UserName.Trim();
    private string ActorRoles => string.Join(",", currentUser.Roles
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Select(value => value.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(value => value));

    public async Task<CivilEngineeringDesignLookupsDto> GetLookupsAsync(
        Guid projectId,
        CancellationToken token = default)
    {
        await RequireProjectAsync(projectId);
        var members = await db.ProjectMembers.AsNoTracking()
            .Where(value => value.TenantId == TenantId && value.ProjectId == projectId
                            && value.IsActive && !value.IsDeleted
                            && (value.Role == CivilEngineeringAccessControlRegistry.SupervisingEngineerRole
                                || value.Role == CivilEngineeringAccessControlRegistry.CivilEngineerRole
                                || value.Role == CivilEngineeringAccessControlRegistry.DraftsmanRole))
            .Join(
                db.Users.AsNoTracking().Where(value => value.TenantId == TenantId && value.IsActive),
                member => member.UserId,
                user => user.Id,
                (member, user) => new CivilEngineeringDesignMemberLookupDto
                {
                    UserId = user.Id,
                    DisplayName = (user.FirstName + " " + user.LastName).Trim(),
                    Role = member.Role
                })
            .OrderBy(value => value.DisplayName)
            .ToListAsync(token);
        var memberIds = members.Select(value => value.UserId).Distinct().ToList();
        var centralRoles = await db.UserRoles.AsNoTracking()
            .Where(value => memberIds.Contains(value.UserId))
            .Join(
                db.Roles.AsNoTracking(),
                userRole => userRole.RoleId,
                role => role.Id,
                (userRole, role) => new { userRole.UserId, role.Name })
            .Where(value => value.Name != null)
            .ToListAsync(token);
        members = members.Where(member => centralRoles.Any(role =>
                role.UserId == member.UserId
                && string.Equals(role.Name, member.Role, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        var informationSourceSections = await db.Sections.AsNoTracking()
            .Where(value => value.TenantId == TenantId && value.IsActive && !value.IsDeleted
                            && value.Department.TenantId == TenantId
                            && value.Department.IsActive && !value.Department.IsDeleted)
            .OrderBy(value => value.Department.Name)
            .ThenBy(value => value.Name)
            .Select(value => new CivilEngineeringDesignSectionLookupDto
            {
                SectionId = value.Id,
                DepartmentId = value.DepartmentId,
                Label = value.Department.Name + " / " + value.Name
            })
            .ToListAsync(token);
        var now = DateTime.UtcNow;
        var engineeringCategories = await db.ProjectCatalogEntries.AsNoTracking()
            .Where(value => value.TenantId == TenantId && !value.IsDeleted && value.IsActive
                            && value.CatalogType == ProjectCatalogDefaults.CivilEngineeringCategories
                            && (!value.EffectiveFrom.HasValue || value.EffectiveFrom <= now)
                            && (!value.EffectiveTo.HasValue || value.EffectiveTo >= now))
            .OrderBy(value => value.SortOrder).ThenBy(value => value.Name)
            .Select(value => new CivilEngineeringDesignSourceLookupDto
            {
                Id = value.Id,
                Label = value.Code + " - " + value.Name
            })
            .ToListAsync(token);
        var capitalProjects = await db.CapitalProjects.AsNoTracking()
            .Where(value => value.TenantId == TenantId && !value.IsDeleted
                            && value.Status == ProjectStatus.Approved)
            .OrderBy(value => value.ProjectCode)
            .Select(value => new CivilEngineeringDesignSourceLookupDto
            {
                Id = value.Id,
                Label = value.ProjectCode + " - " + value.Name
            })
            .Take(250)
            .ToListAsync(token);
        var estateManagedAssets = await db.EstateManagedAssets.AsNoTracking()
            .Where(value => value.TenantId == TenantId && !value.IsDeleted)
            .OrderBy(value => value.AssetCode).ThenBy(value => value.Name)
            .Select(value => new CivilEngineeringDesignSourceLookupDto
            {
                Id = value.Id,
                ProjectId = value.ProjectId,
                Label = value.AssetCode + " - " + value.Name
            })
            .Take(250)
            .ToListAsync(token);
        var maintenanceEscalations = await db.CivilEngineeringMaintenanceIntakes.AsNoTracking()
            .Where(value => value.TenantId == TenantId && !value.IsDeleted
                            && value.ProjectId == projectId
                            && value.Source == CivilEngineeringRequestSource.Maintenance
                            && value.Status != CivilEngineeringMaintenanceIntakeStatuses.Assessed)
            .OrderByDescending(value => value.CreatedAt)
            .Select(value => new CivilEngineeringDesignSourceLookupDto
            {
                Id = value.Id,
                ProjectId = value.ProjectId,
                Label = value.IntakeNumber + " - " + value.Title
            })
            .Take(250)
            .ToListAsync(token);
        var propertyDevelopmentNeeds = await db.EstateManagedAssets.AsNoTracking()
            .Where(value => value.TenantId == TenantId && !value.IsDeleted
                            && value.IsReadyForProjectManagement
                            && (!value.ProjectId.HasValue || value.ProjectId == projectId))
            .OrderBy(value => value.AssetCode).ThenBy(value => value.Name)
            .Select(value => new CivilEngineeringDesignSourceLookupDto
            {
                Id = value.Id,
                ProjectId = value.ProjectId,
                Label = value.AssetCode + " - " + value.Name
            })
            .Take(250)
            .ToListAsync(token);
        var planningConditions = await db.ProjectCivilDevelopmentApprovalFiles.AsNoTracking()
            .Where(value => value.TenantId == TenantId && !value.IsDeleted && value.ProjectId == projectId)
            .OrderByDescending(value => value.CreatedAt)
            .Select(value => new CivilEngineeringDesignSourceLookupDto
            {
                Id = value.Id,
                ProjectId = value.ProjectId,
                Label = value.FileNumber + " - " + value.ApplicationReference
            })
            .Take(250)
            .ToListAsync(token);
        var managementDirectives = await db.CentralDocumentVersions.AsNoTracking()
            .Include(value => value.DocumentRecord)
            .Where(CentralDocumentEvidenceRules.CurrentPublished())
            .Where(value => value.TenantId == TenantId
                            && value.DocumentRecord.MetadataTemplateCode == "TDC-CIV-ENGINEERING-FILE")
            .OrderByDescending(value => value.PublishedAt ?? value.CreatedAt)
            .Select(value => new CivilEngineeringDesignSourceDocumentLookupDto
            {
                CentralDocumentRecordId = value.DocumentRecordId,
                CentralDocumentVersionId = value.Id,
                Label = value.DocumentRecord.DocumentReference + " - " + value.DocumentRecord.Title
            })
            .Take(250)
            .ToListAsync(token);
        var defectMonitoring = await db.ProjectDefectLiabilityCases.AsNoTracking()
            .Where(value => value.TenantId == TenantId && !value.IsDeleted && value.ProjectId == projectId
                            && value.Status != "Closed" && value.Status != "Resolved")
            .OrderByDescending(value => value.ReportedDate)
            .Select(value => new CivilEngineeringDesignSourceLookupDto
            {
                Id = value.Id,
                ProjectId = value.ProjectId,
                Label = value.Title
            })
            .Take(250)
            .ToListAsync(token);
        var infrastructureRequests = await db.ProjectIssues.AsNoTracking()
            .Where(value => value.TenantId == TenantId && !value.IsDeleted && value.ProjectId == projectId
                            && value.Status != "Closed" && value.Status != "Resolved")
            .OrderByDescending(value => value.CreatedAt)
            .Select(value => new CivilEngineeringDesignSourceLookupDto
            {
                Id = value.Id,
                ProjectId = value.ProjectId,
                Label = value.Title
            })
            .Take(250)
            .ToListAsync(token);
        return new CivilEngineeringDesignLookupsDto
        {
            SupervisingCivilEngineers = members.Where(value =>
                value.Role == CivilEngineeringAccessControlRegistry.SupervisingEngineerRole).ToList(),
            CivilEngineers = members.Where(value =>
                value.Role == CivilEngineeringAccessControlRegistry.CivilEngineerRole).ToList(),
            Draftsmen = members.Where(value =>
                value.Role == CivilEngineeringAccessControlRegistry.DraftsmanRole).ToList(),
            InformationSourceSections = informationSourceSections,
            WorkClassifications = Enum.GetValues<CivilEngineeringWorkClassification>(),
            EngineeringCategories = engineeringCategories,
            EstateManagedAssets = estateManagedAssets,
            ApprovedCapitalProjects = capitalProjects,
            MaintenanceEscalations = maintenanceEscalations,
            PropertyDevelopmentNeeds = propertyDevelopmentNeeds,
            PlanningConditions = planningConditions,
            ManagementDirectives = managementDirectives,
            DefectMonitoringCases = defectMonitoring,
            InfrastructureImprovementRequests = infrastructureRequests
        };
    }

    public async Task<IReadOnlyList<CivilEngineeringDesignCaseDto>> ListAsync(
        Guid projectId,
        CancellationToken token = default)
    {
        await RequireProjectAsync(projectId);
        var values = await Query(false)
            .Where(value => value.ProjectId == projectId)
            .OrderByDescending(value => value.CreatedAt)
            .ToListAsync(token);
        return values.Select(Map).ToList();
    }

    public async Task<CivilEngineeringDesignCaseDto> GetAsync(
        Guid id,
        CancellationToken token = default)
    {
        var value = await RequiredAsync(id, false, token);
        await RequireProjectAsync(value.ProjectId);
        return Map(value);
    }

    public async Task<CivilEngineeringDesignCaseDto> CreateAsync(
        CreateCivilEngineeringDesignCaseRequest request,
        string correlationId,
        CancellationToken token = default)
    {
        if (request.ClientRequestId == Guid.Empty || request.ProjectId == Guid.Empty
            || request.SupervisingCivilEngineerUserId == Guid.Empty)
            throw Validation("Select a project and Supervising Civil Engineer and provide a client request identifier.");
        var initiationSource = request.InitiationSource
            ?? throw Validation("Select the controlled source that authorizes this Engineering Case.");
        var initiationSourceId = request.InitiationSourceId
            ?? throw Validation("Select the authoritative source record for this Engineering Case.");
        if (request.EngineeringCategoryId == Guid.Empty)
            throw Validation("Select the controlled Civil Engineering category.");
        var workClassification = request.WorkClassification
            ?? throw Validation("Select the controlled Civil Engineering work classification.");
        var title = RequiredText(request.Title, 3, 200, "Title");
        var directive = RequiredText(request.Directive, 10, 4000, "Directive");
        var scopeSummary = RequiredText(request.ScopeSummary, 3, 4000, "Scope summary");
        var constraintSummary = RequiredText(request.ConstraintSummary, 3, 4000, "Constraint summary");
        var riskSummary = RequiredText(request.RiskSummary, 3, 4000, "Risk summary");
        var recommendation = RequiredText(request.Recommendation, 3, 4000, "Recommendation");

        var requestHash = Hash(new
        {
            request.ProjectId,
            initiationSource,
            initiationSourceId,
            request.InitiationSourceDocumentVersionId,
            request.EstateManagedAssetId,
            request.EngineeringCategoryId,
            workClassification,
            Title = title,
            Directive = directive,
            ScopeSummary = scopeSummary,
            ConstraintSummary = constraintSummary,
            RiskSummary = riskSummary,
            Recommendation = recommendation,
            request.SupervisingCivilEngineerUserId,
            DueAt = Utc(request.DueAt)
        });
        var retry = await Query(false).SingleOrDefaultAsync(value =>
            value.ClientRequestId == request.ClientRequestId, token);
        if (retry is not null)
        {
            if (!FixedEquals(retry.RequestHash, requestHash)) throw RetryConflict();
            return Map(retry);
        }

        await RequireProjectAsync(request.ProjectId);
        await RequireCurrentActorRoleAsync(
            request.ProjectId,
            CivilEngineeringAccessControlRegistry.HeadRole,
            token);
        await RequireProjectMemberRoleAsync(
            request.ProjectId,
            request.SupervisingCivilEngineerUserId,
            CivilEngineeringAccessControlRegistry.SupervisingEngineerRole,
            token);
        CivilEngineeringDesignWorkflowRules.EnsureDistinctAssignments(
            UserId,
            request.SupervisingCivilEngineerUserId,
            null,
            null);
        var initiationPolicy = await ResolveInitiationPolicyAsync(DateTime.UtcNow, request.ProjectId, workClassification, token);
        await RequireEngineeringCategoryAsync(request.EngineeringCategoryId, token);
        var estateAsset = await RequireEstateManagedAssetAsync(request.EstateManagedAssetId, initiationPolicy.RequirePropertyReference, initiationPolicy.RequireLocationReference, token);
        var source = await RequireInitiationSourceAsync(
            initiationSource,
            initiationSourceId,
            request.InitiationSourceDocumentVersionId,
            request.ProjectId,
            estateAsset?.Id,
            token);
        var policy = await ResolvePolicyAsync(DateTime.UtcNow, token);
        var now = DateTime.UtcNow;
        var value = new ProjectCivilDesignCase
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            ProjectId = request.ProjectId,
            InitiationSource = initiationSource,
            InitiationSourceId = source.Id,
            InitiationSourceDocumentVersionId = source.DocumentVersionId,
            InitiationSourceReference = source.Reference,
            EstateManagedAssetId = estateAsset?.Id,
            EngineeringCategoryId = request.EngineeringCategoryId,
            WorkClassification = workClassification,
            ScopeSummary = scopeSummary,
            ConstraintSummary = constraintSummary,
            RiskSummary = riskSummary,
            Recommendation = recommendation,
            ClientRequestId = request.ClientRequestId,
            RequestHash = requestHash,
            ReferenceNumber = $"CIV-DES-{now:yyMMdd}-{request.ClientRequestId:N}"[..25].ToUpperInvariant(),
            Title = title,
            Directive = directive,
            ConfigurationProfileId = policy.ProfileId,
            ConfigurationDecisionId = policy.DecisionId,
            ApprovalWorkflowDefinitionId = policy.WorkflowDefinitionId,
            PolicyHash = policy.PolicyHash,
            RequireSiteReconnaissance = policy.RequireSiteReconnaissance,
            RequireVersionedReview = policy.RequireVersionedReview,
            RequireHodApproval = policy.RequireHodApproval,
            RequirePlanningGisValidation = initiationPolicy.RequirePlanningGisValidation,
            HodUserId = UserId,
            SupervisingCivilEngineerUserId = request.SupervisingCivilEngineerUserId,
            CurrentAssigneeUserId = UserId,
            CurrentDueAt = Utc(request.DueAt),
            CreatedByUserId = UserId,
            CorrelationId = NormalizeCorrelation(correlationId),
            CreatedAt = now,
            CreatedBy = UserName,
            CreatedById = UserId
        };
        db.ProjectCivilDesignCases.Add(value);
        AddRevision(
            value,
            CivilEngineeringAuditEventMap.CreateDesignDirective,
            CivilEngineeringDesignStages.DraftDirective,
            CivilEngineeringDesignStages.DraftDirective,
            directive,
            null,
            Snapshot(value),
            correlationId);
        AddAudit(value, CivilEngineeringAuditEventMap.CreateDesignDirective, null, Snapshot(value), correlationId);
        await SaveAsync(token);
        return await GetAsync(value.Id, token);
    }

    public async Task<CivilEngineeringDesignCaseDto> TransitionAsync(
        Guid id,
        CivilEngineeringDesignTransitionRequest request,
        string correlationId,
        CancellationToken token = default)
    {
        if (request.ClientRequestId == Guid.Empty)
            throw Validation("A client request identifier is required.");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var value = await RequiredAsync(id, true, token);
        await RequireProjectAsync(value.ProjectId);
        var reason = Clean(request.Reason, 2000);
        var evidenceInput = request.Evidence
            .OrderBy(item => item.EvidenceType, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.CentralDocumentVersionId)
            .Select(item => new
            {
                item.CentralDocumentRecordId,
                item.CentralDocumentVersionId,
                EvidenceType = item.EvidenceType?.Trim()
            }).ToList();
        var mutationHash = Hash(new
        {
            request.Action,
            request.AssigneeUserId,
            DueAt = Utc(request.DueAt),
            Reason = reason,
            Evidence = evidenceInput
        });
        if (IsMutationRetry(value, request.ClientRequestId, mutationHash))
        {
            await transaction.RollbackAsync(token);
            return Map(value);
        }
        CivilEngineeringDesignTransitionDefinition transition;
        try { transition = CivilEngineeringDesignWorkflowRules.GetRequired(value.Stage, request.Action); }
        catch (InvalidOperationException exception) { throw Validation(exception.Message); }
        if (transition.RequiresReason && (reason is null || reason.Length < 5))
            throw Validation("A reason of at least 5 characters is required for this action.");
        if (request.Action == CivilEngineeringDesignAction.AssignCivilEngineer && value.RequireSiteReconnaissance)
        {
            var completedReconnaissance = await db.ProjectCivilReconnaissanceReports.AsNoTracking()
                .Include(item => item.Items.Where(line => !line.IsDeleted))
                .Where(item => item.TenantId == TenantId && item.DesignCaseId == value.Id
                               && item.Status == CivilEngineeringReconnaissanceStatuses.Completed
                               && !item.IsDeleted)
                .OrderByDescending(item => item.CompletedAt)
                .FirstOrDefaultAsync(token);
            if (completedReconnaissance is null)
                throw Validation("Complete the governed site reconnaissance before assigning the Civil Engineer.");
            if (completedReconnaissance.Items.Any(item =>
                    item.Kind == CivilEngineeringReconnaissanceItemKind.Constraint
                    && item.BlocksDesign
                    && item.ResolutionStatus == CivilEngineeringConstraintResolutionStatus.Open))
                throw Conflict("The completed site reconnaissance still contains an open design-blocking constraint.");
        }
        if (request.Action == CivilEngineeringDesignAction.AssignCivilEngineer && value.RequirePlanningGisValidation)
        {
            var approvedPlanningGis = await db.ProjectCivilPlanningGisValidations.AsNoTracking().AnyAsync(item =>
                item.TenantId == TenantId && item.DesignCaseId == value.Id && !item.IsDeleted
                && item.Status == CivilEngineeringPlanningGisValidationStatus.Approved, token);
            if (!approvedPlanningGis)
                throw Validation("An independent approved Planning/GIS validation is required before assigning the Civil Engineer.");
        }
        CheckVersion(value.RowVersion, request.RowVersion);
        await RequireTransitionActorAsync(value, transition.Actor, token);

        if (transition.AssigneeRole is not null)
        {
            if (transition.Action == CivilEngineeringDesignAction.AssignCivilEngineer)
            {
                var blockingInputs = await db.ProjectRfis.AsNoTracking().CountAsync(item =>
                    item.TenantId == TenantId
                    && item.CivilDesignCaseId == value.Id
                    && item.BlocksCivilDesignReadiness
                    && item.Status != ProjectRfiStatuses.Closed
                    && !item.IsDeleted,
                    token);
                if (blockingInputs > 0)
                    throw Conflict($"{blockingInputs} required cross-section information request(s) must be accepted before assigning the Civil Engineer.");
            }
            var assignee = request.AssigneeUserId
                ?? throw Validation($"Select a project member with the {transition.AssigneeRole} role.");
            await RequireProjectMemberRoleAsync(value.ProjectId, assignee, transition.AssigneeRole, token);
            if (transition.Action == CivilEngineeringDesignAction.AssignCivilEngineer)
                value.CivilEngineerUserId = assignee;
            else if (transition.Action == CivilEngineeringDesignAction.AssignDraftsman)
                value.DraftsmanUserId = assignee;
            CivilEngineeringDesignWorkflowRules.EnsureDistinctAssignments(
                value.HodUserId,
                value.SupervisingCivilEngineerUserId,
                value.CivilEngineerUserId,
                value.DraftsmanUserId);
        }
        else if (request.AssigneeUserId.HasValue)
        {
            throw Validation("This action does not accept an assignee override.");
        }

        var terminal = transition.ToStage is CivilEngineeringDesignStages.Approved
            or CivilEngineeringDesignStages.Rejected
            or CivilEngineeringDesignStages.Cancelled;
        if (!terminal && (!request.DueAt.HasValue || Utc(request.DueAt) <= DateTime.UtcNow))
            throw Validation("Select a future due date for the next routed task.");

        var requiresEvidence = transition.RequiredEvidenceType is not null
            && (transition.RequiredEvidenceType != CivilEngineeringDesignEvidenceTypes.Reconnaissance
                || value.RequireSiteReconnaissance)
            && (transition.RequiredEvidenceType == CivilEngineeringDesignEvidenceTypes.Directive
                || transition.RequiredEvidenceType == CivilEngineeringDesignEvidenceTypes.Reconnaissance
                || value.RequireVersionedReview);
        if (requiresEvidence && !request.Evidence.Any(item =>
                string.Equals(item.EvidenceType?.Trim(), transition.RequiredEvidenceType, StringComparison.OrdinalIgnoreCase)))
            throw Validation($"Select current published central-DMS {transition.RequiredEvidenceType} evidence before continuing.");
        var evidence = await ValidateEvidenceAsync(value, request.Evidence, token);
        DateTime? governedApprovalAt = null;
        if (request.Action == CivilEngineeringDesignAction.SubmitPackage)
            governedApprovalAt = await RequireCompleteGovernedPackageAsync(value, request.Evidence, token);
        if (transition.AdvancesSharedWorkflow)
            await RequireCurrentSceChecklistAsync(value, evidence, governedApprovalAt, token);

        var before = Snapshot(value);
        var fromStage = value.Stage;
        var toStage = transition.ToStage;
        if (transition.StartsSharedWorkflow)
        {
            var result = await workflow.SubmitAsync(
                CivilEngineeringWorkflowBindingRegistry.DesignReview,
                value.Id,
                value.ApprovalWorkflowDefinitionId);
            if (!result.ExecutionResult.Success)
                throw Conflict(result.ExecutionResult.Message ?? "The Civil design approval workflow could not be started.");
            workflowAdapters.GetAdapter(CivilEngineeringWorkflowBindingRegistry.DesignReview)
                .ApplySubmitOutcome(value, result.Outcome, UserId);
            value.WorkflowInstanceId = result.ExecutionResult.WorkflowInstanceId;
            value.SubmittedById = UserId;
            value.SubmittedAt = DateTime.UtcNow;
        }
        else if (transition.AdvancesSharedWorkflow)
        {
            if (!value.WorkflowInstanceId.HasValue)
                throw Conflict("The shared Civil design review workflow has not been started.");
            if (!await workflow.CanUserApproveAsync(
                    CivilEngineeringWorkflowBindingRegistry.DesignReview,
                    value.Id,
                    UserId))
                throw new UnauthorizedAccessException("You are not assigned to the current SCE design review step.");
            var result = await workflow.ProcessApprovalAsync(
                CivilEngineeringWorkflowBindingRegistry.DesignReview,
                value.Id,
                UserId,
                "Approve",
                reason);
            if (!result.ExecutionResult.Success)
                throw Conflict(result.ExecutionResult.Message ?? "The SCE design review checklist could not be approved.");
            if (value.RequireHodApproval && result.Outcome != WorkflowOutcome.Pending)
                throw Conflict("The configured Civil design workflow must continue to HOD approval after SCE review.");
        }
        else if (transition.CompletesSharedWorkflow)
        {
            if (!value.WorkflowInstanceId.HasValue)
                throw Conflict("The shared Civil design approval workflow has not been started.");
            if (!await workflow.CanUserApproveAsync(
                    CivilEngineeringWorkflowBindingRegistry.DesignReview,
                    value.Id,
                    UserId))
                throw new UnauthorizedAccessException("You are not assigned to the current Civil design approval step.");
            var workflowAction = request.Action == CivilEngineeringDesignAction.Approve ? "Approve" : "Reject";
            var result = await workflow.ProcessApprovalAsync(
                CivilEngineeringWorkflowBindingRegistry.DesignReview,
                value.Id,
                UserId,
                workflowAction,
                reason);
            if (!result.ExecutionResult.Success)
                throw Conflict(result.ExecutionResult.Message ?? "The Civil design approval action could not be processed.");
            if (request.Action == CivilEngineeringDesignAction.Reject && result.Outcome != WorkflowOutcome.Rejected)
                throw Conflict("The shared workflow did not return a rejected outcome.");
            if (request.Action == CivilEngineeringDesignAction.Approve && result.Outcome == WorkflowOutcome.Rejected)
                throw Conflict("The shared workflow returned a rejected outcome for an approval action.");
            workflowAdapters.GetAdapter(CivilEngineeringWorkflowBindingRegistry.DesignReview)
                .ApplyApprovalOutcome(value, result.Outcome, UserId, reason);
            if (request.Action == CivilEngineeringDesignAction.Approve && result.Outcome != WorkflowOutcome.Approved)
                toStage = CivilEngineeringDesignStages.HodFinalReview;
            if (result.Outcome == WorkflowOutcome.Approved)
            {
                value.ApprovedById = UserId;
                value.ApprovedAt = DateTime.UtcNow;
            }
        }
        else if (transition.ToStage == CivilEngineeringDesignStages.Cancelled)
        {
            value.Status = "Cancelled";
            value.ApprovalStatus = "Cancelled";
        }
        else
        {
            value.Status = "InProgress";
            value.ApprovalStatus = "Draft";
            value.ApprovedById = null;
            value.ApprovedAt = null;
            value.RejectionReason = null;
        }

        value.Stage = toStage;
        value.CurrentAssigneeUserId = ResolveNextAssignee(value, toStage);
        value.CurrentDueAt = terminal ? null : Utc(request.DueAt);
        value.LastMutationClientRequestId = request.ClientRequestId;
        value.LastMutationRequestHash = mutationHash;
        value.CorrelationId = NormalizeCorrelation(correlationId);
        value.UpdatedAt = DateTime.UtcNow;
        value.UpdatedBy = UserName;
        value.LastModifiedById = UserId;
        foreach (var item in evidence)
        {
            value.Evidence.Add(new ProjectCivilDesignEvidence
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                DesignCaseId = value.Id,
                CentralDocumentRecordId = item.DocumentRecordId,
                CentralDocumentVersionId = item.Id,
                EvidenceType = request.Evidence.Single(input =>
                    input.CentralDocumentVersionId == item.Id).EvidenceType.Trim(),
                StageSnapshot = fromStage,
                LinkedById = UserId,
                LinkedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = UserName,
                CreatedById = UserId
            });
        }

        var action = AuditAction(request.Action);
        AddRevision(value, action, fromStage, toStage, reason, before, Snapshot(value), correlationId);
        AddAudit(value, action, before, Snapshot(value), correlationId);
        await SaveAsync(token);
        await transaction.CommitAsync(token);
        return await GetAsync(value.Id, token);
    }

    public async Task<IReadOnlyList<CivilEngineeringDesignRevisionDto>> HistoryAsync(
        Guid id,
        CancellationToken token = default)
    {
        var value = await RequiredAsync(id, false, token);
        await RequireProjectAsync(value.ProjectId);
        return await db.ProjectCivilDesignRevisions.AsNoTracking()
            .Where(item => item.TenantId == TenantId && item.DesignCaseId == id && !item.IsDeleted)
            .OrderBy(item => item.CreatedAt)
            .Select(item => new CivilEngineeringDesignRevisionDto
            {
                Id = item.Id,
                Action = item.Action,
                FromStage = item.FromStage,
                ToStage = item.ToStage,
                ActorUserId = item.ActorUserId,
                ActorName = item.ActorName,
                ActorRoles = item.ActorRoles,
                CorrelationId = item.CorrelationId,
                Reason = item.Reason,
                Timestamp = item.CreatedAt
            })
            .ToListAsync(token);
    }

    private IQueryable<ProjectCivilDesignCase> Query(bool tracked)
    {
        var query = db.ProjectCivilDesignCases
            .Include(value => value.EstateManagedAsset)
            .Include(value => value.EngineeringCategory)
            .Include(value => value.Evidence.Where(item => !item.IsDeleted))
                .ThenInclude(value => value.CentralDocumentRecord)
            .Include(value => value.Evidence.Where(item => !item.IsDeleted))
                .ThenInclude(value => value.CentralDocumentVersion)
            .Where(value => value.TenantId == TenantId && !value.IsDeleted);
        return tracked ? query : query.AsNoTracking();
    }

    private async Task<ProjectCivilDesignCase> RequiredAsync(Guid id, bool tracked, CancellationToken token)
        => await Query(tracked).SingleOrDefaultAsync(value => value.Id == id, token)
           ?? throw new CivilEngineeringDesignNotFoundException("The Civil design case was not found.");

    private async Task RequireProjectAsync(Guid projectId)
    {
        if (projectId == Guid.Empty || await projectService.GetProjectByIdAsync(projectId) is null)
            throw new UnauthorizedAccessException("You are not permitted to access the selected project.");
    }

    private async Task RequireCurrentActorRoleAsync(Guid projectId, string role, CancellationToken token)
    {
        if (!currentUser.Roles.Any(value => string.Equals(value, role, StringComparison.OrdinalIgnoreCase)))
            throw new UnauthorizedAccessException($"The {role} role is required for this action.");
        await RequireProjectMemberRoleAsync(projectId, UserId, role, token);
    }

    private async Task RequireTransitionActorAsync(
        ProjectCivilDesignCase value,
        CivilEngineeringDesignActor actor,
        CancellationToken token)
    {
        var (expectedUserId, requiredRole) = actor switch
        {
            CivilEngineeringDesignActor.Hod => (value.HodUserId, CivilEngineeringAccessControlRegistry.HeadRole),
            CivilEngineeringDesignActor.SupervisingCivilEngineer => (value.SupervisingCivilEngineerUserId, CivilEngineeringAccessControlRegistry.SupervisingEngineerRole),
            CivilEngineeringDesignActor.CivilEngineer => (value.CivilEngineerUserId ?? Guid.Empty, CivilEngineeringAccessControlRegistry.CivilEngineerRole),
            CivilEngineeringDesignActor.Draftsman => (value.DraftsmanUserId ?? Guid.Empty, CivilEngineeringAccessControlRegistry.DraftsmanRole),
            _ => throw Validation("The Civil design actor is not supported.")
        };
        if (expectedUserId == Guid.Empty || expectedUserId != UserId)
            throw new UnauthorizedAccessException("Only the assigned user can perform this Civil design action.");
        if (!currentUser.Roles.Any(role => string.Equals(role, requiredRole, StringComparison.OrdinalIgnoreCase)))
            throw new UnauthorizedAccessException($"The {requiredRole} role is required for this action.");
        await RequireProjectMemberRoleAsync(value.ProjectId, UserId, requiredRole, token);
    }

    private async Task RequireProjectMemberRoleAsync(
        Guid projectId,
        Guid userId,
        string role,
        CancellationToken token)
    {
        var userActive = await db.Users.AsNoTracking().AnyAsync(value =>
            value.TenantId == TenantId && value.Id == userId && value.IsActive, token);
        var memberActive = await db.ProjectMembers.AsNoTracking().AnyAsync(value =>
            value.TenantId == TenantId && value.ProjectId == projectId && value.UserId == userId
            && value.IsActive && !value.IsDeleted && value.Role == role, token);
        var centralRoleActive = await db.UserRoles.AsNoTracking()
            .Where(value => value.UserId == userId)
            .Join(
                db.Roles.AsNoTracking(),
                userRole => userRole.RoleId,
                centralRole => centralRole.Id,
                (_, centralRole) => centralRole.Name)
            .AnyAsync(name => name == role, token);
        if (!userActive || !memberActive || !centralRoleActive)
            throw Validation($"Select an active current-tenant project member with the {role} role.");
    }

    private async Task<InitiationPolicy> ResolveInitiationPolicyAsync(
        DateTime atUtc,
        Guid projectId,
        CivilEngineeringWorkClassification classification,
        CancellationToken token)
    {
        var at = atUtc.Kind == DateTimeKind.Utc ? atUtc : atUtc.ToUniversalTime();
        var profiles = await db.CivilEngineeringConfigurationProfiles.AsNoTracking()
            .Where(value => value.TenantId == TenantId && !value.IsDeleted
                            && value.LifecycleStatus == CivilEngineeringConfigurationProfileStatus.Published
                            && value.EffectiveFrom <= at
                            && (!value.EffectiveTo.HasValue || value.EffectiveTo >= at))
            .OrderByDescending(value => value.IsDefault)
            .ThenByDescending(value => value.Version)
            .Take(2)
            .ToListAsync(token);
        if (profiles.Count == 0)
            throw Validation("No Published Civil Engineering configuration is effective for this date.");
        if (profiles.Count > 1 && profiles[0].IsDefault == profiles[1].IsDefault)
            throw Conflict("More than one Civil Engineering configuration is effective for this date.");
        var profile = profiles[0];
        var decision = await db.CivilEngineeringConfigurationDecisions.AsNoTracking()
            .SingleOrDefaultAsync(value => value.TenantId == TenantId && value.ProfileId == profile.Id
                                           && value.ConfigurationKey == "CIV-CFG-002" && !value.IsDeleted, token)
            ?? throw Validation("The effective Civil Engineering configuration has no CIV-CFG-002 classification decision.");
        if (decision.Status != CivilEngineeringConfigurationDecisionStatus.Approved
            || decision.ApprovalStatus != CivilEngineeringConfigurationApprovalStatus.Approved
            || decision.EvidenceStatus != CivilEngineeringConfigurationEvidenceStatus.Verified
            || (decision.EffectiveFrom.HasValue && decision.EffectiveFrom > at)
            || (decision.EffectiveTo.HasValue && decision.EffectiveTo < at))
            throw Validation("CIV-CFG-002 is not approved, verified, and effective for this date.");
        CivilEngineeringWorkClassificationValue configured;
        try
        {
            configured = JsonSerializer.Deserialize<CivilEngineeringWorkClassificationValue>(decision.ValueJson, JsonOptions)
                         ?? throw new JsonException();
        }
        catch (JsonException)
        {
            throw Conflict("CIV-CFG-002 contains invalid classification policy data.");
        }
        if (!configured.AllowedClassifications.Contains(classification))
            throw Validation("The selected work classification is not enabled by CIV-CFG-002.");
        var project = await db.Projects.AsNoTracking().SingleOrDefaultAsync(value =>
                value.TenantId == TenantId && value.Id == projectId && !value.IsDeleted,
            token) ?? throw new UnauthorizedAccessException("You are not permitted to access the selected project.");
        if (configured.ProjectTypeIds.Count == 0 || !project.ProjectTypeId.HasValue
            || !configured.ProjectTypeIds.Contains(project.ProjectTypeId.Value))
            throw Validation("The selected project type is not enabled by CIV-CFG-002.");
        return new InitiationPolicy(
            configured.RequirePropertyReference,
            configured.RequireLocationReference,
            configured.RequirePlanningGisValidation);
    }

    private async Task RequireEngineeringCategoryAsync(Guid categoryId, CancellationToken token)
    {
        var now = DateTime.UtcNow;
        if (!await db.ProjectCatalogEntries.AsNoTracking().AnyAsync(value =>
                value.TenantId == TenantId && value.Id == categoryId && !value.IsDeleted && value.IsActive
                && value.CatalogType == ProjectCatalogDefaults.CivilEngineeringCategories
                && (!value.EffectiveFrom.HasValue || value.EffectiveFrom <= now)
                && (!value.EffectiveTo.HasValue || value.EffectiveTo >= now), token))
            throw Validation("Select an active Civil Engineering category from the controlled project catalogue.");
    }

    private async Task<EstateManagedAsset?> RequireEstateManagedAssetAsync(
        Guid? id,
        bool required,
        bool requireLocation,
        CancellationToken token)
    {
        if (!id.HasValue || id == Guid.Empty)
        {
            if (required) throw Validation("Select the controlled property or site required by CIV-CFG-002.");
            return null;
        }
        var asset = await db.EstateManagedAssets.AsNoTracking().SingleOrDefaultAsync(value =>
            value.TenantId == TenantId && value.Id == id && !value.IsDeleted, token)
            ?? throw Validation("The selected property or site is not available in this tenant.");
        if (requireLocation && string.IsNullOrWhiteSpace(asset.Location)
            && string.IsNullOrWhiteSpace(asset.GisFeatureId)
            && string.IsNullOrWhiteSpace(asset.BoundaryCoordinates))
            throw Validation("The selected property or site has no controlled location or GIS reference required by CIV-CFG-002.");
        return asset;
    }

    private async Task<InitiationSourceValue> RequireInitiationSourceAsync(
        CivilEngineeringWorksInitiationSource source,
        Guid sourceId,
        Guid? sourceDocumentVersionId,
        Guid projectId,
        Guid? estateManagedAssetId,
        CancellationToken token)
    {
        if (sourceId == Guid.Empty) throw Validation("Select the authoritative source record.");
        InitiationSourceValue value = source switch
        {
            CivilEngineeringWorksInitiationSource.ApprovedCapitalProject => await RequireCapitalProjectAsync(sourceId, token),
            CivilEngineeringWorksInitiationSource.MaintenanceEscalation => await RequireMaintenanceEscalationAsync(sourceId, token),
            CivilEngineeringWorksInitiationSource.PropertyDevelopmentNeed => await RequirePropertyDevelopmentNeedAsync(sourceId, token),
            CivilEngineeringWorksInitiationSource.PlanningCondition => await RequirePlanningConditionAsync(sourceId, token),
            CivilEngineeringWorksInitiationSource.ManagementDirective => await RequireManagementDirectiveAsync(sourceId, sourceDocumentVersionId, token),
            CivilEngineeringWorksInitiationSource.DefectMonitoring => await RequireDefectMonitoringAsync(sourceId, token),
            CivilEngineeringWorksInitiationSource.InfrastructureImprovementRequest => await RequireInfrastructureRequestAsync(sourceId, token),
            _ => throw Validation("The selected Works/Engineering Case source is not supported.")
        };
        if (value.ProjectId.HasValue && value.ProjectId != projectId)
            throw Validation("The selected source record belongs to a different project.");
        if (value.EstateManagedAssetId.HasValue && estateManagedAssetId != value.EstateManagedAssetId)
            throw Validation("The selected source record does not match the selected property or site.");
        if (source is CivilEngineeringWorksInitiationSource.PropertyDevelopmentNeed
            or CivilEngineeringWorksInitiationSource.PlanningCondition
            && !estateManagedAssetId.HasValue)
            throw Validation("Select the property or site associated with the selected source record.");
        return value;
    }

    private async Task<InitiationSourceValue> RequireCapitalProjectAsync(Guid id, CancellationToken token)
    {
        var value = await db.CapitalProjects.AsNoTracking().SingleOrDefaultAsync(item =>
            item.TenantId == TenantId && item.Id == id && !item.IsDeleted && item.Status == ProjectStatus.Approved, token)
            ?? throw Validation("The selected capital project is not approved in this tenant.");
        return new InitiationSourceValue(value.Id, null, null, value.ProjectCode + " - " + value.Name);
    }

    private async Task<InitiationSourceValue> RequireMaintenanceEscalationAsync(Guid id, CancellationToken token)
    {
        var value = await db.CivilEngineeringMaintenanceIntakes.AsNoTracking().SingleOrDefaultAsync(item =>
            item.TenantId == TenantId && item.Id == id && !item.IsDeleted
            && item.Source == CivilEngineeringRequestSource.Maintenance
            && item.Status != CivilEngineeringMaintenanceIntakeStatuses.Assessed, token)
            ?? throw Validation("The selected maintenance escalation is no longer available.");
        return new InitiationSourceValue(value.Id, value.ProjectId, value.EstateManagedAssetId,
            value.IntakeNumber + " - " + value.Title);
    }

    private async Task<InitiationSourceValue> RequirePropertyDevelopmentNeedAsync(Guid id, CancellationToken token)
    {
        var value = await db.EstateManagedAssets.AsNoTracking().SingleOrDefaultAsync(item =>
            item.TenantId == TenantId && item.Id == id && !item.IsDeleted && item.IsReadyForProjectManagement, token)
            ?? throw Validation("The selected property is not available as a controlled development need.");
        return new InitiationSourceValue(value.Id, value.ProjectId, value.Id, value.AssetCode + " - " + value.Name);
    }

    private async Task<InitiationSourceValue> RequirePlanningConditionAsync(Guid id, CancellationToken token)
    {
        var value = await db.ProjectCivilDevelopmentApprovalFiles.AsNoTracking().SingleOrDefaultAsync(item =>
            item.TenantId == TenantId && item.Id == id && !item.IsDeleted, token)
            ?? throw Validation("The selected planning/development condition is no longer available.");
        return new InitiationSourceValue(value.Id, value.ProjectId, value.EstateManagedAssetId,
            value.FileNumber + " - " + value.ApplicationReference);
    }

    private async Task<InitiationSourceValue> RequireManagementDirectiveAsync(
        Guid recordId,
        Guid? versionId,
        CancellationToken token)
    {
        if (!versionId.HasValue || versionId == Guid.Empty)
            throw Validation("Select the current Published central-DMS version for the management directive.");
        var value = await db.CentralDocumentVersions.AsNoTracking().Include(item => item.DocumentRecord)
            .Where(CentralDocumentEvidenceRules.CurrentPublished())
            .SingleOrDefaultAsync(item => item.TenantId == TenantId && item.Id == versionId, token)
            ?? throw Validation("The selected management directive version is not current and Published.");
        if (value.DocumentRecordId != recordId
            || !string.Equals(value.DocumentRecord.MetadataTemplateCode, "TDC-CIV-ENGINEERING-FILE", StringComparison.OrdinalIgnoreCase))
            throw Validation("The management directive must use the current Published Civil Engineering central-DMS document.");
        return new InitiationSourceValue(recordId, null, null,
            value.DocumentRecord.DocumentReference + " - " + value.DocumentRecord.Title, value.Id);
    }

    private async Task<InitiationSourceValue> RequireDefectMonitoringAsync(Guid id, CancellationToken token)
    {
        var value = await db.ProjectDefectLiabilityCases.AsNoTracking().SingleOrDefaultAsync(item =>
            item.TenantId == TenantId && item.Id == id && !item.IsDeleted
            && item.Status != "Closed" && item.Status != "Resolved", token)
            ?? throw Validation("The selected defect-monitoring case is no longer active.");
        return new InitiationSourceValue(value.Id, value.ProjectId, null, value.Title);
    }

    private async Task<InitiationSourceValue> RequireInfrastructureRequestAsync(Guid id, CancellationToken token)
    {
        var value = await db.ProjectIssues.AsNoTracking().SingleOrDefaultAsync(item =>
            item.TenantId == TenantId && item.Id == id && !item.IsDeleted
            && item.Status != "Closed" && item.Status != "Resolved", token)
            ?? throw Validation("The selected infrastructure-improvement request is no longer active.");
        return new InitiationSourceValue(value.Id, value.ProjectId, null, value.Title);
    }

    private async Task<IReadOnlyList<CentralDocumentVersion>> ValidateEvidenceAsync(
        ProjectCivilDesignCase value,
        IReadOnlyList<CivilEngineeringDesignEvidenceRequest> input,
        CancellationToken token)
    {
        if (input.Count == 0) return [];
        if (input.Any(item => item.CentralDocumentRecordId == Guid.Empty
                              || item.CentralDocumentVersionId == Guid.Empty
                              || !CivilEngineeringDesignEvidenceTypes.All.Contains(item.EvidenceType?.Trim() ?? string.Empty)))
            throw Validation("Every evidence item must select a supported type and current central-DMS document version.");
        if (input.Select(item => new { item.CentralDocumentVersionId, Type = item.EvidenceType.Trim().ToUpperInvariant() })
            .Distinct().Count() != input.Count)
            throw Validation("The same document version and evidence type cannot be selected more than once.");
        var ids = input.Select(item => item.CentralDocumentVersionId).Distinct().ToList();
        var versions = await db.CentralDocumentVersions.AsNoTracking()
            .Include(item => item.DocumentRecord)
            .Where(CentralDocumentEvidenceRules.CurrentPublished())
            .Where(item => item.TenantId == TenantId && ids.Contains(item.Id))
            .ToListAsync(token);
        if (versions.Count != ids.Count || input.Any(item => versions.All(version =>
                version.Id != item.CentralDocumentVersionId
                || version.DocumentRecordId != item.CentralDocumentRecordId)))
            throw Validation("One or more selected central-DMS versions are not current, published, or owned by this tenant.");
        if (input.Any(item => value.Evidence.Any(existing =>
                !existing.IsDeleted
                && existing.CentralDocumentVersionId == item.CentralDocumentVersionId
                && string.Equals(existing.EvidenceType, item.EvidenceType.Trim(), StringComparison.OrdinalIgnoreCase))))
            throw Conflict("One or more evidence versions are already linked to this Civil design case for the selected type.");
        return versions;
    }

    private async Task RequireCurrentSceChecklistAsync(
        ProjectCivilDesignCase value,
        IReadOnlyCollection<CentralDocumentVersion> incomingEvidence,
        DateTime? governedApprovalAt,
        CancellationToken token)
    {
        if (!value.WorkflowInstanceId.HasValue)
            throw Conflict("The shared Civil design review workflow has not been started.");
        var step = await db.WorkflowStepInstances.AsNoTracking()
            .Where(item => item.TenantId == TenantId
                           && item.WorkflowInstanceId == value.WorkflowInstanceId
                           && !item.IsDeleted
                           && (item.Status == WorkflowStepInstanceStatus.Pending
                               || item.Status == WorkflowStepInstanceStatus.InProgress))
            .OrderByDescending(item => item.StartedDate ?? item.CreatedDate)
            .FirstOrDefaultAsync(token)
            ?? throw Conflict("The current SCE design review step is not available.");
        var evidenceNotBefore = value.Evidence
            .Where(item => !item.IsDeleted
                           && (item.EvidenceType == CivilEngineeringDesignEvidenceTypes.Design
                               || item.EvidenceType == CivilEngineeringDesignEvidenceTypes.Drawing
                               || item.EvidenceType == CivilEngineeringDesignEvidenceTypes.Reconnaissance))
            .Select(item => item.CentralDocumentVersion.PublishedAt
                            ?? item.CentralDocumentVersion.UpdatedAt
                            ?? item.CentralDocumentVersion.CreatedAt)
            .Concat(incomingEvidence.Select(item => item.PublishedAt ?? item.UpdatedAt ?? item.CreatedAt))
            .Concat(governedApprovalAt.HasValue ? [governedApprovalAt.Value] : [])
            .DefaultIfEmpty(value.SubmittedAt ?? value.CreatedAt)
            .Max()
            .ToUniversalTime();
        var responses = WorkflowChecklistEvidenceValidator.ReadResponses(step.ResultData);
        var errors = CivilEngineeringDesignReviewChecklistPolicy.ValidateCurrentResponses(
            responses,
            UserId,
            evidenceNotBefore);
        if (errors.Count > 0)
            throw Validation(string.Join(" ", errors));
    }

    private async Task<DateTime?> RequireCompleteGovernedPackageAsync(
        ProjectCivilDesignCase value,
        IReadOnlyCollection<CivilEngineeringDesignEvidenceRequest> incomingEvidence,
        CancellationToken token)
    {
        if (incomingEvidence.GroupBy(item => item.EvidenceType.Trim(), StringComparer.OrdinalIgnoreCase)
            .Any(group => group.Count() > 1))
            throw Validation("Select only one current DMS version for each package evidence type.");
        var frozenDecision = await db.CivilEngineeringConfigurationDecisions.AsNoTracking()
            .SingleOrDefaultAsync(item => item.TenantId == TenantId
                                          && item.Id == value.ConfigurationDecisionId
                                          && item.ProfileId == value.ConfigurationProfileId
                                          && item.ConfigurationKey == "CIV-CFG-003"
                                          && !item.IsDeleted, token)
            ?? throw Conflict("The frozen CIV-CFG-003 design policy is unavailable.");
        CivilEngineeringDesignReviewValue configured;
        try
        {
            configured = JsonSerializer.Deserialize<CivilEngineeringDesignReviewValue>(
                             frozenDecision.ValueJson,
                             JsonOptions)
                         ?? throw new JsonException();
        }
        catch (JsonException)
        {
            throw Conflict("The frozen CIV-CFG-003 design policy contains invalid package controls.");
        }

        var documents = await db.ProjectCivilEngineeringDocuments.AsNoTracking()
            .Where(item => item.TenantId == TenantId
                           && item.DesignCaseId == value.Id
                           && item.Status == CivilEngineeringDocumentStatus.Approved
                           && !item.IsDeleted)
            .Select(item => new
            {
                item.Discipline,
                item.Status,
                item.CentralDocumentVersionId,
                item.ReviewedAt
            })
            .ToListAsync(token);
        var versionIds = documents.Select(item => item.CentralDocumentVersionId).Distinct().ToList();
        var currentPublishedIds = versionIds.Count == 0
            ? new HashSet<Guid>()
            : (await db.CentralDocumentVersions.AsNoTracking()
                    .Where(CentralDocumentEvidenceRules.CurrentPublished())
                    .Where(item => item.TenantId == TenantId && versionIds.Contains(item.Id))
                    .Select(item => item.Id)
                    .ToListAsync(token))
                .ToHashSet();
        var governed = documents.Select(item => new CivilEngineeringGovernedPackageDocument(
            item.Discipline,
            item.Status,
            item.CentralDocumentVersionId,
            currentPublishedIds.Contains(item.CentralDocumentVersionId),
            item.ReviewedAt)).ToList();
        var incomingTypes = incomingEvidence.Select(item => item.EvidenceType.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var packageEvidence = value.Evidence.Where(item => !item.IsDeleted && !incomingTypes.Contains(item.EvidenceType))
            .GroupBy(item => item.EvidenceType.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(item => item.LinkedAt).First())
            .Select(item => new CivilEngineeringPackageEvidence(
                item.EvidenceType,
                item.CentralDocumentVersionId))
            .Concat(incomingEvidence.Select(item => new CivilEngineeringPackageEvidence(
                item.EvidenceType,
                item.CentralDocumentVersionId)))
            .ToList();
        var readiness = CivilEngineeringDesignPackageReadinessPolicy.Validate(
            configured.RequiredDisciplines,
            governed,
            packageEvidence);
        if (!readiness.IsReady)
            throw Validation(string.Join(" ", readiness.Errors));
        return readiness.LatestGovernedApprovalAt;
    }

    private async Task<Policy> ResolvePolicyAsync(DateTime atUtc, CancellationToken token)
    {
        var at = atUtc.Kind == DateTimeKind.Utc ? atUtc : atUtc.ToUniversalTime();
        var profiles = await db.CivilEngineeringConfigurationProfiles.AsNoTracking()
            .Where(value => value.TenantId == TenantId && !value.IsDeleted
                            && value.LifecycleStatus == CivilEngineeringConfigurationProfileStatus.Published
                            && value.EffectiveFrom <= at
                            && (!value.EffectiveTo.HasValue || value.EffectiveTo >= at))
            .OrderByDescending(value => value.IsDefault)
            .ThenByDescending(value => value.Version)
            .Take(2)
            .ToListAsync(token);
        if (profiles.Count == 0)
            throw Validation("No Published Civil Engineering configuration is effective for this date.");
        if (profiles.Count > 1 && profiles[0].IsDefault == profiles[1].IsDefault)
            throw Conflict("More than one Civil Engineering configuration is effective for this date.");
        var profile = profiles[0];
        var decision = await db.CivilEngineeringConfigurationDecisions.AsNoTracking()
            .SingleOrDefaultAsync(value => value.TenantId == TenantId && value.ProfileId == profile.Id
                                           && value.ConfigurationKey == "CIV-CFG-003" && !value.IsDeleted, token)
            ?? throw Validation("The effective Civil Engineering configuration has no CIV-CFG-003 decision.");
        if (decision.Status != CivilEngineeringConfigurationDecisionStatus.Approved
            || decision.ApprovalStatus != CivilEngineeringConfigurationApprovalStatus.Approved
            || decision.EvidenceStatus != CivilEngineeringConfigurationEvidenceStatus.Verified
            || (decision.EffectiveFrom.HasValue && decision.EffectiveFrom > at)
            || (decision.EffectiveTo.HasValue && decision.EffectiveTo < at))
            throw Validation("CIV-CFG-003 is not approved, verified, and effective for this date.");
        CivilEngineeringDesignReviewValue configured;
        try
        {
            configured = JsonSerializer.Deserialize<CivilEngineeringDesignReviewValue>(decision.ValueJson, JsonOptions)
                         ?? throw new JsonException();
        }
        catch (JsonException)
        {
            throw Conflict("CIV-CFG-003 contains invalid design workflow policy data.");
        }
        if (configured.WorkflowDefinitionId == Guid.Empty)
            throw Conflict("CIV-CFG-003 has no configured workflow definition.");
        var validWorkflow = await db.WorkflowDefinitions.AsNoTracking()
            .Include(value => value.EntityType)
            .Include(value => value.Steps)
            .SingleOrDefaultAsync(value => value.TenantId == TenantId
                                           && value.Id == configured.WorkflowDefinitionId
                                           && !value.IsDeleted
                                           && value.IsActive
                                           && value.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Published
                                           && !value.EntityType.IsDeleted
                                           && value.EntityType.IsActive
                                           && value.EntityType.Code == CivilEngineeringWorkflowBindingRegistry.DesignReview,
                token);
        if (validWorkflow is null)
            throw Validation($"The CIV-CFG-003 workflow must be active, Published, and bound to {CivilEngineeringWorkflowBindingRegistry.DesignReview}.");
        var workflowErrors = CivilEngineeringDesignReviewChecklistPolicy.Validate(
            validWorkflow,
            configured.RequireHodApproval);
        if (workflowErrors.Count > 0)
            throw Validation(string.Join(" ", workflowErrors));
        return new Policy(
            profile.Id,
            decision.Id,
            configured.WorkflowDefinitionId,
            configured.RequireSiteReconnaissance,
            configured.RequireVersionedReview,
            configured.RequireHodApproval,
            Hash(new
            {
                Profile = profile.Id,
                profile.Version,
                Decision = decision.Id,
                decision.ValueJson,
                configured.WorkflowDefinitionId,
                EntityType = CivilEngineeringWorkflowBindingRegistry.DesignReview
            }));
    }

    private static Guid? ResolveNextAssignee(ProjectCivilDesignCase value, string stage) => stage switch
    {
        CivilEngineeringDesignStages.SceInformationGathering => value.SupervisingCivilEngineerUserId,
        CivilEngineeringDesignStages.CivilEngineerDesign => value.CivilEngineerUserId,
        CivilEngineeringDesignStages.SceDesignReview => value.SupervisingCivilEngineerUserId,
        CivilEngineeringDesignStages.Drafting => value.DraftsmanUserId,
        CivilEngineeringDesignStages.SceDrawingReview => value.SupervisingCivilEngineerUserId,
        CivilEngineeringDesignStages.HodFinalReview => value.HodUserId,
        _ => null
    };

    private static string AuditAction(CivilEngineeringDesignAction action) => action switch
    {
        CivilEngineeringDesignAction.DirectToSce => CivilEngineeringAuditEventMap.UpdateDesignTaskAssignment,
        CivilEngineeringDesignAction.AssignCivilEngineer => CivilEngineeringAuditEventMap.UpdateDesignTaskAssignment,
        CivilEngineeringDesignAction.SubmitDesign => CivilEngineeringAuditEventMap.SubmitDesignForReview,
        CivilEngineeringDesignAction.ReturnDesign => CivilEngineeringAuditEventMap.RejectDesign,
        CivilEngineeringDesignAction.AssignDraftsman => CivilEngineeringAuditEventMap.ApproveDesign,
        CivilEngineeringDesignAction.SubmitDrawings => CivilEngineeringAuditEventMap.SubmitDrawingForReview,
        CivilEngineeringDesignAction.ReturnDrawings => CivilEngineeringAuditEventMap.RejectDrawing,
        CivilEngineeringDesignAction.SubmitPackage => CivilEngineeringAuditEventMap.SubmitDesignPackage,
        CivilEngineeringDesignAction.Approve => CivilEngineeringAuditEventMap.ApproveDesignPackage,
        CivilEngineeringDesignAction.Reject => CivilEngineeringAuditEventMap.RejectDesignPackage,
        CivilEngineeringDesignAction.Cancel => CivilEngineeringAuditEventMap.UpdateDesignTaskAssignment,
        _ => throw Validation("The Civil design action is not supported.")
    };

    private void AddRevision(
        ProjectCivilDesignCase value,
        string action,
        string fromStage,
        string toStage,
        string? reason,
        object? before,
        object after,
        string correlationId) => value.Revisions.Add(new ProjectCivilDesignRevision
    {
        Id = Guid.NewGuid(),
        TenantId = TenantId,
        DesignCaseId = value.Id,
        Action = action,
        FromStage = fromStage,
        ToStage = toStage,
        ActorUserId = UserId,
        ActorName = UserName,
        ActorRoles = ActorRoles,
        CorrelationId = NormalizeCorrelation(correlationId),
        Reason = reason,
        BeforeJson = before is null ? null : JsonSerializer.Serialize(before, JsonOptions),
        AfterJson = JsonSerializer.Serialize(after, JsonOptions),
        CreatedAt = DateTime.UtcNow,
        CreatedBy = UserName,
        CreatedById = UserId
    });

    private void AddAudit(
        ProjectCivilDesignCase value,
        string action,
        object? before,
        object after,
        string correlationId) => db.AuditLogs.Add(new AuditLog
    {
        TenantId = TenantId,
        UserId = UserId,
        Username = UserName,
        Action = action,
        Resource = nameof(ProjectCivilDesignCase),
        ResourceId = value.Id.ToString(),
        OldValues = before is null ? null : JsonSerializer.Serialize(before, JsonOptions),
        NewValues = JsonSerializer.Serialize(new
        {
            correlationId = NormalizeCorrelation(correlationId),
            value = after
        }, JsonOptions),
        IpAddress = currentUser.IpAddress,
        UserAgent = currentUser.UserAgent,
        Timestamp = DateTime.UtcNow,
        CreatedAt = DateTime.UtcNow,
        CreatedBy = UserName,
        CreatedById = UserId
    });

    private static object Snapshot(ProjectCivilDesignCase value) => new
    {
        value.Id,
        value.ProjectId,
        value.InitiationSource,
        value.InitiationSourceId,
        value.InitiationSourceDocumentVersionId,
        value.InitiationSourceReference,
        value.EstateManagedAssetId,
        value.EngineeringCategoryId,
        value.WorkClassification,
        value.ScopeSummary,
        value.ConstraintSummary,
        value.RiskSummary,
        value.Recommendation,
        value.ReferenceNumber,
        value.Stage,
        value.Status,
        value.ApprovalStatus,
        value.ConfigurationProfileId,
        value.ConfigurationDecisionId,
        value.ApprovalWorkflowDefinitionId,
        value.PolicyHash,
        value.HodUserId,
        value.SupervisingCivilEngineerUserId,
        value.CivilEngineerUserId,
        value.DraftsmanUserId,
        value.CurrentAssigneeUserId,
        value.CurrentDueAt,
        value.WorkflowInstanceId,
        value.ApprovedById,
        value.ApprovedAt,
        value.RejectionReason
    };

    private static CivilEngineeringDesignCaseDto Map(ProjectCivilDesignCase value) => new()
    {
        Id = value.Id,
        ProjectId = value.ProjectId,
        InitiationSource = value.InitiationSource,
        InitiationSourceId = value.InitiationSourceId,
        InitiationSourceDocumentVersionId = value.InitiationSourceDocumentVersionId,
        InitiationSourceReference = value.InitiationSourceReference,
        EstateManagedAssetId = value.EstateManagedAssetId,
        EstateManagedAssetLabel = value.EstateManagedAsset is null
            ? null
            : value.EstateManagedAsset.AssetCode + " - " + value.EstateManagedAsset.Name,
        EngineeringCategoryId = value.EngineeringCategoryId,
        EngineeringCategoryLabel = value.EngineeringCategory is null
            ? null
            : value.EngineeringCategory.Code + " - " + value.EngineeringCategory.Name,
        WorkClassification = value.WorkClassification,
        ScopeSummary = value.ScopeSummary,
        ConstraintSummary = value.ConstraintSummary,
        RiskSummary = value.RiskSummary,
        Recommendation = value.Recommendation,
        ReferenceNumber = value.ReferenceNumber,
        Title = value.Title,
        Directive = value.Directive,
        Stage = value.Stage,
        Status = value.Status,
        ApprovalStatus = value.ApprovalStatus,
        RequirePlanningGisValidation = value.RequirePlanningGisValidation,
        HodUserId = value.HodUserId,
        SupervisingCivilEngineerUserId = value.SupervisingCivilEngineerUserId,
        CivilEngineerUserId = value.CivilEngineerUserId,
        DraftsmanUserId = value.DraftsmanUserId,
        CurrentAssigneeUserId = value.CurrentAssigneeUserId,
        CurrentDueAt = value.CurrentDueAt,
        WorkflowInstanceId = value.WorkflowInstanceId,
        ApprovedAt = value.ApprovedAt,
        RejectionReason = value.RejectionReason,
        RowVersion = Convert.ToBase64String(value.RowVersion),
        Evidence = value.Evidence.Where(item => !item.IsDeleted).OrderBy(item => item.LinkedAt)
            .Select(item => new CivilEngineeringDesignEvidenceDto
            {
                Id = item.Id,
                CentralDocumentRecordId = item.CentralDocumentRecordId,
                CentralDocumentVersionId = item.CentralDocumentVersionId,
                DocumentReference = item.CentralDocumentRecord?.DocumentReference ?? string.Empty,
                DocumentTitle = item.CentralDocumentRecord?.Title ?? string.Empty,
                VersionNumber = item.CentralDocumentVersion?.VersionNumber ?? string.Empty,
                EvidenceType = item.EvidenceType,
                StageSnapshot = item.StageSnapshot,
                LinkedAt = item.LinkedAt
            }).ToList()
    };

    private static bool IsMutationRetry(ProjectCivilDesignCase value, Guid clientRequestId, string requestHash)
    {
        if (value.LastMutationClientRequestId != clientRequestId) return false;
        if (!FixedEquals(value.LastMutationRequestHash, requestHash)) throw RetryConflict();
        return true;
    }

    private async Task SaveAsync(CancellationToken token)
    {
        try { await db.SaveChangesAsync(token); }
        catch (DbUpdateConcurrencyException) { throw Conflict("The Civil design case changed concurrently. Refresh and retry."); }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException sqlException
                                                  && sqlException.Number is >= 51901 and <= 51938)
        { throw Conflict(sqlException.Message); }
        catch (DbUpdateException exception) when (exception.InnerException?.Message.Contains("unique", StringComparison.OrdinalIgnoreCase) == true)
        { throw Conflict("A Civil design case already uses this request or reference."); }
    }

    private static void CheckVersion(byte[] current, string supplied)
    {
        byte[] expected;
        try { expected = Convert.FromBase64String(supplied); }
        catch (FormatException) { throw Conflict("The row version is invalid. Refresh and retry."); }
        if (!CryptographicOperations.FixedTimeEquals(current, expected))
            throw Conflict("The Civil design case changed. Refresh and retry.");
    }

    private static string RequiredText(string? value, int min, int max, string label)
    {
        var clean = value?.Trim();
        if (string.IsNullOrWhiteSpace(clean) || clean.Length < min || clean.Length > max)
            throw Validation($"{label} must contain between {min} and {max} characters.");
        return clean;
    }

    private static string? Clean(string? value, int max) => string.IsNullOrWhiteSpace(value)
        ? null
        : value.Trim().Length <= max
            ? value.Trim()
            : throw Validation($"Text cannot exceed {max} characters.");
    private static DateTime? Utc(DateTime? value) => !value.HasValue
        ? null
        : value.Value.Kind == DateTimeKind.Utc
            ? value.Value
            : value.Value.ToUniversalTime();
    private static string NormalizeCorrelation(string? value) => string.IsNullOrWhiteSpace(value)
        ? Guid.NewGuid().ToString("N")
        : value.Trim().Length <= 100 ? value.Trim() : value.Trim()[..100];
    private static string Hash(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        JsonSerializer.Serialize(value, JsonOptions)))).ToLowerInvariant();
    private static bool FixedEquals(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right)) return false;
        var a = Encoding.UTF8.GetBytes(left);
        var b = Encoding.UTF8.GetBytes(right);
        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }

    private static CivilEngineeringDesignValidationException Validation(string message) => new(message);
    private static CivilEngineeringDesignConflictException Conflict(string message) => new(message);
    private static CivilEngineeringDesignConflictException RetryConflict() =>
        Conflict("The client request identifier was already used with different content.");

    private sealed record Policy(
        Guid ProfileId,
        Guid DecisionId,
        Guid WorkflowDefinitionId,
        bool RequireSiteReconnaissance,
        bool RequireVersionedReview,
        bool RequireHodApproval,
        string PolicyHash);

    private sealed record InitiationPolicy(
        bool RequirePropertyReference,
        bool RequireLocationReference,
        bool RequirePlanningGisValidation);

    private sealed record InitiationSourceValue(
        Guid Id,
        Guid? ProjectId,
        Guid? EstateManagedAssetId,
        string Reference,
        Guid? DocumentVersionId = null);
}
