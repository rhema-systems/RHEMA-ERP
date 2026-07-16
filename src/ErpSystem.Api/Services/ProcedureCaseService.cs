using System.Text.Json;
using ErpSystem.Core.DTOs.Procedures;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities.Procedures;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Estate;
using ErpSystem.Core.Interfaces.Legal;
using ErpSystem.Core.Interfaces.Planning;
using ErpSystem.Core.Interfaces.Procedures;
using ErpSystem.Core.Interfaces.Workflow;
using ErpSystem.Core.Services.Workflow;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services;

public sealed class ProcedureCaseService : IProcedureCaseService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ILegalProcedureCatalogService _legalCatalog;
    private readonly IEstateProcedureCatalogService _estateCatalog;
    private readonly IFacilitiesProcedureCatalogService _facilitiesCatalog;
    private readonly IPropertyManagementProcedureCatalogService _propertyManagementCatalog;
    private readonly IPlanningProcedureCatalogService _planningCatalog;
    private readonly IWorkflowEngine _workflowEngine;

    public ProcedureCaseService(
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        ILegalProcedureCatalogService legalCatalog,
        IEstateProcedureCatalogService estateCatalog,
        IFacilitiesProcedureCatalogService facilitiesCatalog,
        IPropertyManagementProcedureCatalogService propertyManagementCatalog,
        IPlanningProcedureCatalogService planningCatalog,
        IWorkflowEngine workflowEngine)
    {
        _db = db;
        _currentUser = currentUser;
        _legalCatalog = legalCatalog;
        _estateCatalog = estateCatalog;
        _facilitiesCatalog = facilitiesCatalog;
        _propertyManagementCatalog = propertyManagementCatalog;
        _planningCatalog = planningCatalog;
        _workflowEngine = workflowEngine;
    }

    public async Task<IReadOnlyList<ProcedureCaseSummaryDto>> GetCasesAsync(string? module, string? entityType, bool mineOnly)
    {
        var tenantId = RequireTenantId();
        var roles = _currentUser.Roles.ToArray();

        var query = _db.ProcedureCases
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted);

        if (!string.IsNullOrWhiteSpace(module))
        {
            query = query.Where(item => item.Module == module);
        }

        if (!string.IsNullOrWhiteSpace(entityType))
        {
            query = query.Where(item => item.EntityType == entityType);
        }

        if (mineOnly && !IsWorkflowAdmin())
        {
            query = query.Where(item => item.CurrentAssignedRole == null || roles.Any(role => item.CurrentAssignedRole!.Contains(role)));
        }

        var cases = await query
            .OrderByDescending(item => item.UpdatedAt ?? item.CreatedAt)
            .Take(100)
            .ToListAsync();

        return cases.Select(ToSummaryDto).ToList();
    }

    public async Task<ProcedureCaseDetailDto?> GetCaseAsync(Guid id)
    {
        var procedureCase = await LoadCaseAsync(id, asTracking: false);
        return procedureCase is null ? null : ToDetailDto(procedureCase);
    }

    public async Task<ProcedureCaseDetailDto> CreateCaseAsync(CreateProcedureCaseRequest request)
    {
        var tenantId = RequireTenantId();
        var userId = RequireUserId();
        var now = DateTime.UtcNow;
        var module = NormalizeModule(request.Module);
        var entityType = request.EntityType.Trim();
        var workspace = await BuildWorkspaceSeedAsync(module, entityType);
        var firstStage = workspace.Stages.FirstOrDefault() ?? new StageSeed(0, "Open", null, null, null, []);

        var procedureCase = new ProcedureCase
        {
            TenantId = tenantId,
            Module = module,
            EntityType = entityType,
            Title = string.IsNullOrWhiteSpace(request.Title) ? workspace.Title : request.Title.Trim(),
            ReferenceNumber = request.ReferenceNumber,
            ApplicantName = request.ApplicantName,
            SourceDepartment = request.SourceDepartment,
            ReceivedDate = request.ReceivedDate,
            Description = request.Description,
            Status = "Open",
            CurrentStageIndex = firstStage.Index,
            CurrentStageName = firstStage.Name,
            CurrentStageOwner = firstStage.Owner,
            CurrentAssignedRole = firstStage.AssignedRole ?? firstStage.Owner,
            WorkflowDefinitionId = workspace.WorkflowDefinitionId,
            WorkflowStepId = firstStage.WorkflowStepId,
            OpenedById = userId,
            LastActionById = userId,
            CreatedById = userId,
            CreatedBy = _currentUser.UserName,
            CreatedAt = now,
            UpdatedAt = now
        };

        foreach (var field in workspace.Fields)
        {
            var value = request.FieldValues != null && request.FieldValues.TryGetValue(field.Key, out var submittedValue)
                ? submittedValue
                : null;

            procedureCase.Fields.Add(new ProcedureCaseField
            {
                TenantId = tenantId,
                Key = field.Key,
                Label = field.Label,
                FieldType = field.FieldType,
                Value = value,
                OptionsJson = field.Options is null ? null : JsonSerializer.Serialize(field.Options),
                CreatedById = userId,
                CreatedAt = now
            });
        }

        foreach (var stage in workspace.Stages)
        {
            foreach (var checklistItem in stage.Checklist)
            {
                procedureCase.ChecklistItems.Add(new ProcedureCaseChecklistItem
                {
                    TenantId = tenantId,
                    StageIndex = stage.Index,
                    StageName = stage.Name,
                    Text = checklistItem,
                    CreatedById = userId,
                    CreatedAt = now
                });
            }
        }

        foreach (var document in workspace.Documents)
        {
            procedureCase.Documents.Add(new ProcedureCaseDocument
            {
                TenantId = tenantId,
                Name = document.Name,
                RequiredFrom = document.RequiredFrom,
                IsMandatory = document.IsMandatory,
                CreatedById = userId,
                CreatedAt = now
            });
        }

        procedureCase.Activities.Add(Activity(tenantId, userId, procedureCase.Id, "Created", firstStage.Name, "Procedure case opened."));

        _db.ProcedureCases.Add(procedureCase);
        await _db.SaveChangesAsync();

        if (workspace.WorkflowDefinitionName is not null)
        {
            var workflowInstance = await _workflowEngine.StartWorkflowAsync(
                workspace.WorkflowDefinitionName,
                procedureCase.Id,
                userId,
                new
                {
                    procedureCase.Id,
                    procedureCase.Module,
                    procedureCase.EntityType,
                    procedureCase.Title,
                    procedureCase.ReferenceNumber,
                    procedureCase.ApplicantName,
                    procedureCase.SourceDepartment,
                    procedureCase.ReceivedDate
                });

            procedureCase.WorkflowInstanceId = workflowInstance.Id;
            procedureCase.WorkflowDefinitionId = workflowInstance.WorkflowDefinitionId;
            procedureCase.WorkflowStepId = workflowInstance.CurrentStepId;
            procedureCase.Activities.Add(Activity(tenantId, userId, procedureCase.Id, "Workflow started", firstStage.Name, workspace.WorkflowDefinitionName));
            await _db.SaveChangesAsync();
            await SyncCaseFromWorkflowRuntimeAsync(procedureCase.Id, workflowInstance.Id, userId);
        }

        return ToDetailDto((await LoadCaseAsync(procedureCase.Id, asTracking: false))!);
    }

    public async Task<ProcedureCaseDetailDto?> UpdateFieldsAsync(Guid id, UpdateProcedureCaseFieldsRequest request)
    {
        var procedureCase = await LoadCaseAsync(id, asTracking: false);
        if (procedureCase is null)
        {
            return null;
        }

        EnsureCanEdit(procedureCase);
        var tenantId = RequireTenantId();
        var userId = RequireUserId();
        var now = DateTime.UtcNow;

        await _db.ProcedureCases
            .IgnoreQueryFilters()
            .Where(item => item.TenantId == tenantId && item.Id == id && !item.IsDeleted)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.ReferenceNumber, request.ReferenceNumber)
                .SetProperty(item => item.ApplicantName, request.ApplicantName)
                .SetProperty(item => item.SourceDepartment, request.SourceDepartment)
                .SetProperty(item => item.ReceivedDate, request.ReceivedDate)
                .SetProperty(item => item.Description, request.Description)
                .SetProperty(item => item.LastActionById, userId)
                .SetProperty(item => item.UpdatedAt, now));

        foreach (var field in procedureCase.Fields)
        {
            if (request.FieldValues.TryGetValue(field.Key, out var value))
            {
                await _db.ProcedureCaseFields
                    .IgnoreQueryFilters()
                    .Where(item => item.TenantId == tenantId && item.ProcedureCaseId == id && item.Id == field.Id && !item.IsDeleted)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(item => item.Value, value)
                        .SetProperty(item => item.UpdatedAt, now)
                        .SetProperty(item => item.LastModifiedById, userId));
            }
        }

        _db.ProcedureCaseActivities.Add(Activity(tenantId, userId, procedureCase.Id, "Updated intake", procedureCase.CurrentStageName, "Intake fields saved."));
        await _db.SaveChangesAsync();

        return ToDetailDto((await LoadCaseAsync(id, asTracking: false))!);
    }

    public async Task<ProcedureCaseDetailDto?> UpdateChecklistItemAsync(Guid id, Guid checklistItemId, UpdateProcedureCaseChecklistRequest request)
    {
        var procedureCase = await LoadCaseAsync(id, asTracking: false);
        if (procedureCase is null)
        {
            return null;
        }

        EnsureCanEdit(procedureCase);
        var item = procedureCase.ChecklistItems.FirstOrDefault(check => check.Id == checklistItemId);
        if (item is null)
        {
            return null;
        }

        if (item.StageIndex != procedureCase.CurrentStageIndex)
        {
            throw new InvalidOperationException("Only checklist items in the current stage can be changed.");
        }

        var tenantId = RequireTenantId();
        var userId = RequireUserId();
        var now = DateTime.UtcNow;

        await _db.ProcedureCaseChecklistItems
            .IgnoreQueryFilters()
            .Where(check => check.TenantId == tenantId && check.ProcedureCaseId == id && check.Id == checklistItemId && !check.IsDeleted)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(check => check.IsCompleted, request.IsCompleted)
                .SetProperty(check => check.CompletedById, request.IsCompleted ? userId : null)
                .SetProperty(check => check.CompletedAt, request.IsCompleted ? now : null)
                .SetProperty(check => check.UpdatedAt, now)
                .SetProperty(check => check.LastModifiedById, userId));

        await _db.ProcedureCases
            .IgnoreQueryFilters()
            .Where(item => item.TenantId == tenantId && item.Id == id && !item.IsDeleted)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.LastActionById, userId)
                .SetProperty(item => item.UpdatedAt, now));

        _db.ProcedureCaseActivities.Add(Activity(tenantId, userId, procedureCase.Id, request.IsCompleted ? "Completed checklist" : "Reopened checklist", procedureCase.CurrentStageName, item.Text));
        await _db.SaveChangesAsync();

        return ToDetailDto((await LoadCaseAsync(id, asTracking: false))!);
    }

    public async Task<ProcedureCaseDetailDto?> AttachDocumentAsync(Guid id, Guid documentId, AttachProcedureCaseDocumentRequest request)
    {
        var procedureCase = await LoadCaseAsync(id, asTracking: false);
        if (procedureCase is null)
        {
            return null;
        }

        EnsureCanEdit(procedureCase);
        var document = procedureCase.Documents.FirstOrDefault(item => item.Id == documentId);
        if (document is null)
        {
            return null;
        }

        var tenantId = RequireTenantId();
        var userId = RequireUserId();
        var now = DateTime.UtcNow;

        await _db.ProcedureCaseDocuments
            .IgnoreQueryFilters()
            .Where(item => item.TenantId == tenantId && item.ProcedureCaseId == id && item.Id == documentId && !item.IsDeleted)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.FileName, request.FileName)
                .SetProperty(item => item.FileUrl, request.FileUrl)
                .SetProperty(item => item.Notes, request.Notes)
                .SetProperty(item => item.UploadedById, userId)
                .SetProperty(item => item.UploadedAt, now)
                .SetProperty(item => item.UpdatedAt, now)
                .SetProperty(item => item.LastModifiedById, userId));

        await _db.ProcedureCases
            .IgnoreQueryFilters()
            .Where(item => item.TenantId == tenantId && item.Id == id && !item.IsDeleted)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.LastActionById, userId)
                .SetProperty(item => item.UpdatedAt, now));

        _db.ProcedureCaseActivities.Add(Activity(tenantId, userId, procedureCase.Id, "Attached document", procedureCase.CurrentStageName, document.Name));
        await _db.SaveChangesAsync();

        return ToDetailDto((await LoadCaseAsync(id, asTracking: false))!);
    }

    public async Task<ProcedureCaseDetailDto?> CompleteCurrentStageAsync(Guid id, CompleteProcedureCaseStageRequest request)
    {
        var procedureCase = await LoadCaseAsync(id, asTracking: false);
        if (procedureCase is null)
        {
            return null;
        }

        EnsureCanEdit(procedureCase);

        var currentItems = procedureCase.ChecklistItems
            .Where(item => item.StageIndex == procedureCase.CurrentStageIndex)
            .ToList();

        if (currentItems.Any(item => !item.IsCompleted))
        {
            throw new InvalidOperationException("Complete the current stage checklist before submitting the stage.");
        }

        var missingDocuments = procedureCase.Documents
            .Where(item => item.IsMandatory
                && !item.UploadedAt.HasValue
                && string.Equals(item.RequiredFrom, procedureCase.CurrentStageName, StringComparison.OrdinalIgnoreCase))
            .Select(item => item.Name)
            .ToList();

        if (missingDocuments.Count > 0)
        {
            throw new InvalidOperationException($"Upload required document(s) before submitting this stage: {string.Join(", ", missingDocuments)}.");
        }

        var tenantId = RequireTenantId();
        var userId = RequireUserId();
        var now = DateTime.UtcNow;

        _db.ProcedureCaseActivities.Add(Activity(tenantId, userId, procedureCase.Id, "Completed stage", procedureCase.CurrentStageName, request.Notes));

        if (procedureCase.WorkflowInstanceId.HasValue)
        {
            await _db.SaveChangesAsync();
            var result = await _workflowEngine.ExecuteNextStepAsync(procedureCase.WorkflowInstanceId.Value, userId, new
            {
                procedureCase.Id,
                procedureCase.Module,
                procedureCase.EntityType,
                procedureCase.CurrentStageName,
                request.Notes,
                SubmittedAt = now
            });

            if (!result.Success)
            {
                throw new InvalidOperationException(result.Message ?? "Workflow step could not be completed.");
            }

            await SyncCaseFromWorkflowRuntimeAsync(procedureCase.Id, procedureCase.WorkflowInstanceId.Value, userId, request.Notes);
            return ToDetailDto((await LoadCaseAsync(id, asTracking: false))!);
        }

        var stages = await BuildStageSeedsAsync(procedureCase.Module, procedureCase.EntityType);
        var currentPosition = stages.FindIndex(stage => stage.Index == procedureCase.CurrentStageIndex);
        var nextStage = currentPosition >= 0 && currentPosition + 1 < stages.Count ? stages[currentPosition + 1] : null;

        if (nextStage is null)
        {
            await _db.ProcedureCases
                .IgnoreQueryFilters()
                .Where(item => item.TenantId == tenantId && item.Id == id && !item.IsDeleted)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.Status, "Completed")
                    .SetProperty(item => item.CompletedAt, now)
                    .SetProperty(item => item.CurrentAssignedRole, (string?)null)
                    .SetProperty(item => item.CurrentStageOwner, (string?)null)
                    .SetProperty(item => item.LastActionById, userId)
                    .SetProperty(item => item.UpdatedAt, now));
        }
        else
        {
            var nextAssignedRole = nextStage.AssignedRole ?? nextStage.Owner;
            await _db.ProcedureCases
                .IgnoreQueryFilters()
                .Where(item => item.TenantId == tenantId && item.Id == id && !item.IsDeleted)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.Status, "Open")
                    .SetProperty(item => item.CurrentStageIndex, nextStage.Index)
                    .SetProperty(item => item.CurrentStageName, nextStage.Name)
                    .SetProperty(item => item.CurrentStageOwner, nextStage.Owner)
                    .SetProperty(item => item.CurrentAssignedRole, nextAssignedRole)
                    .SetProperty(item => item.WorkflowStepId, nextStage.WorkflowStepId)
                    .SetProperty(item => item.LastActionById, userId)
                    .SetProperty(item => item.UpdatedAt, now));
            _db.ProcedureCaseActivities.Add(Activity(tenantId, userId, procedureCase.Id, "Assigned stage", nextStage.Name, $"Assigned to {nextAssignedRole ?? "unassigned"}."));
        }
        await _db.SaveChangesAsync();

        return ToDetailDto((await LoadCaseAsync(id, asTracking: false))!);
    }

    private async Task<ProcedureCase?> LoadCaseAsync(Guid id, bool asTracking)
    {
        var tenantId = RequireTenantId();
        var query = _db.ProcedureCases
            .Include(item => item.Fields)
            .Include(item => item.ChecklistItems)
            .Include(item => item.Documents)
            .Include(item => item.Activities)
            .Where(item => item.TenantId == tenantId && item.Id == id && !item.IsDeleted);

        if (!asTracking)
        {
            query = query.AsNoTracking();
        }

        return await query.FirstOrDefaultAsync();
    }

    private async Task<WorkspaceSeed> BuildWorkspaceSeedAsync(string module, string entityType)
    {
        var stages = await BuildStageSeedsAsync(module, entityType);
        var (title, fields, documents) = module switch
        {
            "Legal" => BuildLegalSeed(entityType),
            "Estate" => BuildEstateSeed(entityType),
            "PropertyManagement" => BuildPropertyManagementSeed(entityType),
            "Facilities" => BuildFacilitiesSeed(entityType),
            "Planning" => BuildPlanningSeed(entityType),
            _ => throw new InvalidOperationException($"Procedure module '{module}' is not supported.")
        };

        var workflowStage = stages.FirstOrDefault(stage => stage.WorkflowDefinitionId.HasValue);
        if (workflowStage?.WorkflowDefinitionId is { } workflowDefinitionId)
        {
            var workflowDocuments = await BuildWorkflowDocumentSeedsAsync(workflowDefinitionId);
            if (workflowDocuments.Count > 0)
            {
                documents = workflowDocuments;
            }
        }

        return new WorkspaceSeed(title, stages, fields, documents, workflowStage?.WorkflowDefinitionId, workflowStage?.WorkflowDefinitionName);
    }

    private async Task<List<StageSeed>> BuildStageSeedsAsync(string module, string entityType)
    {
        var tenantId = RequireTenantId();
        var workflow = await _db.WorkflowDefinitions
            .AsNoTracking()
            .Include(item => item.EntityType)
            .Include(item => item.Steps)
            .Where(item => item.TenantId == tenantId
                && item.IsActive
                && !item.IsDeleted
                && item.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Published
                && (item.EntityType.Name == entityType || item.EntityType.Code == entityType))
            .OrderByDescending(item => item.PublishedAt ?? item.CreatedAt)
            .ThenByDescending(item => item.Version)
            .FirstOrDefaultAsync();

        if (workflow is not null && workflow.Steps.Count > 0)
        {
            return workflow.Steps
                .OrderBy(item => item.Order)
                .Select((step, index) => new StageSeed(
                    index,
                    step.Name,
                    step.RequiredRole,
                    step.RequiredRole ?? ExtractAssignedRole(step.AssignmentConfiguration) ?? step.Name,
                    step.Id,
                    [step.Description ?? $"Complete {step.Name}."])
                {
                    WorkflowDefinitionId = workflow.Id,
                    WorkflowDefinitionName = workflow.Name
                })
                .ToList();
        }

        return module switch
        {
            "Legal" => _legalCatalog.GetProcedureWorkspace(entityType)?.Stages
                .Select((stage, index) => new StageSeed(index, stage.Name, stage.Owner, stage.Owner, null, stage.Checklist))
                .ToList() ?? [],
            "Estate" => BuildEstateStageSeeds(entityType),
            "PropertyManagement" => _propertyManagementCatalog.GetProcedureWorkspace(entityType)?.Stages
                .Select((stage, index) => new StageSeed(index, stage.Name, stage.Owner, stage.Owner, null, stage.Checklist))
                .ToList() ?? [],
            "Facilities" => _facilitiesCatalog.GetProcedureWorkspace(entityType)?.Stages
                .Select((stage, index) => new StageSeed(index, stage.Name, stage.Owner, stage.Owner, null, stage.Checklist))
                .ToList() ?? [],
            "Planning" => _planningCatalog.GetProcedureWorkspace(entityType)?.Stages
                .Select((stage, index) => new StageSeed(index, stage.Name, stage.Owner, stage.Owner, null, stage.Checklist))
                .ToList() ?? [],
            _ => []
        };
    }

    private (string Title, IReadOnlyList<FieldSeed> Fields, IReadOnlyList<DocumentSeed> Documents) BuildLegalSeed(string entityType)
    {
        var workspace = _legalCatalog.GetProcedureWorkspace(entityType)
            ?? throw new InvalidOperationException($"Legal procedure workspace '{entityType}' was not found.");

        return (
            workspace.Procedure.Title,
            workspace.IntakeFields.Select(item => new FieldSeed(item.Key, item.Label, item.Type, item.Options)).ToList(),
            workspace.RequiredDocuments.Select(item => new DocumentSeed(item.Name, item.RequiredFrom, item.IsMandatory)).ToList());
    }

    private (string Title, IReadOnlyList<FieldSeed> Fields, IReadOnlyList<DocumentSeed> Documents) BuildFacilitiesSeed(string entityType)
    {
        var workspace = _facilitiesCatalog.GetProcedureWorkspace(entityType)
            ?? throw new InvalidOperationException($"Facilities procedure workspace '{entityType}' was not found.");

        return (
            workspace.Procedure.Title,
            workspace.IntakeFields.Select(item => new FieldSeed(item.Key, item.Label, item.Type, item.Options)).ToList(),
            workspace.RequiredDocuments.Select(item => new DocumentSeed(item.Name, item.RequiredFrom, item.IsMandatory)).ToList());
    }

    private (string Title, IReadOnlyList<FieldSeed> Fields, IReadOnlyList<DocumentSeed> Documents) BuildEstateSeed(string entityType)
    {
        var procedure = GetEstateProcedure(entityType);

        return (
            procedure.Title,
            BuildEstateFieldSeeds(procedure),
            BuildEstateDocumentSeeds(procedure));
    }

    private (string Title, IReadOnlyList<FieldSeed> Fields, IReadOnlyList<DocumentSeed> Documents) BuildPropertyManagementSeed(string entityType)
    {
        var workspace = _propertyManagementCatalog.GetProcedureWorkspace(entityType)
            ?? throw new InvalidOperationException($"Property Management procedure workspace '{entityType}' was not found.");

        return (
            workspace.Procedure.Title,
            workspace.IntakeFields.Select(item => new FieldSeed(item.Key, item.Label, item.Type, item.Options)).ToList(),
            workspace.RequiredDocuments.Select(item => new DocumentSeed(item.Name, item.RequiredFrom, item.IsMandatory)).ToList());
    }

    private (string Title, IReadOnlyList<FieldSeed> Fields, IReadOnlyList<DocumentSeed> Documents) BuildPlanningSeed(string entityType)
    {
        var workspace = _planningCatalog.GetProcedureWorkspace(entityType)
            ?? throw new InvalidOperationException($"Planning procedure workspace '{entityType}' was not found.");

        return (
            workspace.Procedure.Title,
            workspace.IntakeFields.Select(item => new FieldSeed(item.Key, item.Label, item.Type, item.Options)).ToList(),
            workspace.RequiredDocuments.Select(item => new DocumentSeed(item.Name, item.RequiredFrom, item.IsMandatory)).ToList());
    }

    private EstateProcedureCatalogItem GetEstateProcedure(string entityType) =>
        _estateCatalog.GetProcedures().FirstOrDefault(item =>
            string.Equals(item.EntityType, entityType, StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException($"Estate procedure workspace '{entityType}' was not found.");

    private List<StageSeed> BuildEstateStageSeeds(string entityType)
    {
        var procedure = GetEstateProcedure(entityType);
        var stageNames = EstateStageNames(procedure.EntityType);

        return stageNames
            .Take(procedure.StageCount)
            .Select((name, index) => new StageSeed(
                index,
                name,
                EstateStageOwner(name),
                EstateStageOwner(name),
                null,
                EstateStageChecklist(procedure.EntityType, name)))
            .ToList();
    }

    private static IReadOnlyList<FieldSeed> BuildEstateFieldSeeds(EstateProcedureCatalogItem procedure) =>
    [
        new("referenceNumber", "Reference number", "text", null),
        new("procedureType", "Procedure", "text", [procedure.Title]),
        new("applicantName", "Applicant / lessee / client name", "text", null),
        new("propertyNumber", "Property / plot / house number", "text", null),
        new("fileReference", "Estate file reference", "text", null),
        new("schedule", "Estate schedule", "select", ["Registry", "Records", "Serviced Plots", "Lands / Partially Serviced", "Housing", "Traditional Lands", "Regularisation", "Facilities"]),
        new("location", "Location / community", "text", null),
        new("sourceDepartment", "Source department", "text", null),
        new("receivedDate", "Received date", "date", null),
        new("assignedOfficer", "Assigned Estate officer", "text", null),
        new("arrearsStatus", "Arrears status", "select", ["Not checked", "No arrears", "Arrears exist", "Waiver / exception approved"]),
        new("feeReference", "Fee / invoice / receipt reference", "text", null),
        new("legalReference", "Legal reference", "text", null),
        new("financeReference", "Finance reference", "text", null),
        new("planningReference", "Planning / site plan reference", "text", null),
        new("dmsFolderReference", "DMS folder reference", "text", null)
    ];

    private static IReadOnlyList<DocumentSeed> BuildEstateDocumentSeeds(EstateProcedureCatalogItem procedure) =>
    [
        new("Application letter / request form", "Applicant / Registry", true),
        new("Property file extract", "Estate Registry / Records", true),
        new("Ownership, tenancy, lease, or allocation evidence", "Applicant / Estate Records", true),
        new("Revenue / arrears / payment confirmation", "Finance / Revenue", false),
        new("Site plan, cadastral plan, layout, or inspection evidence", "Planning / Development / Estate", false),
        new("Approval, recommendation, or routing note", "HOE / EM / EO", true),
        new($"{procedure.Title} output", "Estate Department", true),
        new("Central DMS reference", "Document Mngt", false)
    ];

    private static IReadOnlyList<string> EstateStageNames(string entityType) =>
        entityType switch
        {
            "EstateRegistrySecretariat" => ["Receive and log intake", "Classify file or letter", "Route to responsible officer", "Track movement", "Prepare typing or dispatch action", "Update client", "Close registry action"],
            "EstateRecordsManagement" => ["Receive record request", "Verify estate register or ledger", "Check Revenue and Development consistency", "Prepare amendment or notification", "Review and sign", "Update records", "Notify agencies", "Close records action"],
            "EstateInspection" => ["Receive inspection request", "Assign inspection officer", "Conduct site visit", "Prepare site report", "Submit report"],
            "EstateSearchApplication" => ["Receive search application", "Check arrears and file status", "Review property records", "Prepare search report", "Dispatch search response"],
            "EstateRecordAmendment" => ["Receive amendment request", "Validate supporting declaration", "Check arrears and ownership", "Update Revenue and Estate Records", "Dispatch confirmation"],
            "EstateCertifiedTrueCopy" => ["Receive certified copy request", "Verify file and arrears", "Confirm payment", "Prepare certified copy", "Approve and dispatch"],
            "EstateJointOwnership" => ["Receive addition request", "Verify lease and ownership", "Check arrears and consent", "Route cadastral or legal action", "Prepare deed or variation", "Update records", "Dispatch confirmation"],
            "EstateTransfer" => ["Receive transfer request", "Verify parties and property", "Calculate fees and arrears", "Approve transfer instruction", "Route Legal execution", "Update records", "Dispatch completion"],
            "EstateAssignment" => ["Receive assignment request", "Check consent and draft deed", "Verify arrears and development status", "Approve assignment instruction", "Route Legal registration", "Detach and update records", "Dispatch completion"],
            "EstateLeasePreparation" => ["Receive lease request", "Verify development and property status", "Confirm cadastral requirements", "Prepare invoice instruction", "Confirm payment", "Route Legal preparation", "Update records", "Dispatch lease"],
            "EstateLeaseRenewal" => ["Receive renewal request", "Verify renewal requirements", "Check arrears and term threshold", "Route committee review", "Prepare invoice instruction", "Approve renewal", "Route Legal renewal", "Close renewal"],
            "EstateServicedPlotAllocation" => ["Receive allocation request", "Compile allocation list", "Approve allocation", "Update payment book", "Prepare offer letter", "Prepare right of entry", "Dispatch documents", "Report allocation"],
            "EstateLandsPartiallyServiced" => ["Receive application", "Assess land use and plot details", "Calculate LMF and ground rent", "Prepare proposal letter", "Confirm acceptance and payment", "Prepare offer and right of entry", "Report schedule"],
            "EstateHousingHomeOwnership" => ["Receive housing request", "Verify tenancy and rent position", "Confirm HOS or recognition path", "Route legal or records action", "Confirm payment or conversion", "Prepare offer or rent card", "Update records"],
            "EstateTraditionalLands" => ["Receive stool allocation", "Open file and check prior allocation", "Conduct site visit", "Request site plan", "Prepare proposal letter", "Prepare offer and right of entry"],
            "EstateTenancyRegularisation" => ["Receive regularisation request", "Validate documents and plot number", "Interview applicant", "Check Revenue and Estate Records", "Route committee vetting", "Prepare proposal and fees", "Prepare offer and right of entry"],
            "EstateReportingControls" => ["Collect schedule data", "Validate control checks", "Compile report", "Review exceptions", "Approve report", "Publish controls"],
            _ => ["Receive request", "Validate records", "Check fees and approvals", "Route linked action", "Prepare output", "Update records", "Close case"]
        };

    private static string EstateStageOwner(string stageName)
    {
        if (stageName.Contains("Legal", StringComparison.OrdinalIgnoreCase))
        {
            return "Estate Officer / Legal";
        }

        if (stageName.Contains("payment", StringComparison.OrdinalIgnoreCase)
            || stageName.Contains("arrears", StringComparison.OrdinalIgnoreCase)
            || stageName.Contains("invoice", StringComparison.OrdinalIgnoreCase)
            || stageName.Contains("fees", StringComparison.OrdinalIgnoreCase))
        {
            return "Estate Officer / Finance Revenue";
        }

        if (stageName.Contains("site", StringComparison.OrdinalIgnoreCase)
            || stageName.Contains("planning", StringComparison.OrdinalIgnoreCase)
            || stageName.Contains("cadastral", StringComparison.OrdinalIgnoreCase))
        {
            return "Estate Officer / Planning";
        }

        if (stageName.Contains("Approve", StringComparison.OrdinalIgnoreCase)
            || stageName.Contains("Review", StringComparison.OrdinalIgnoreCase))
        {
            return "HOE / Estate Manager";
        }

        if (stageName.Contains("records", StringComparison.OrdinalIgnoreCase)
            || stageName.Contains("ledger", StringComparison.OrdinalIgnoreCase))
        {
            return "Estate Records";
        }

        return "Estate Officer";
    }

    private static IReadOnlyList<string> EstateStageChecklist(string entityType, string stageName) =>
    [
        $"Complete {stageName.ToLowerInvariant()} for {entityType}.",
        "Confirm Estate file reference, property number, applicant, and source department.",
        "Attach or reference required Estate/DMS documents.",
        "Record linked Finance, Legal, Planning, Project, Property Management, or Facilities references where applicable."
    ];

    private async Task<IReadOnlyList<DocumentSeed>> BuildWorkflowDocumentSeedsAsync(Guid workflowDefinitionId)
    {
        var steps = await _db.WorkflowSteps
            .AsNoTracking()
            .Where(item => item.WorkflowDefinitionId == workflowDefinitionId && !item.IsDeleted)
            .OrderBy(item => item.Order)
            .ThenBy(item => item.Name)
            .ToListAsync();

        var documents = new List<DocumentSeed>();
        foreach (var step in steps)
        {
            var config = DeserializeStepConfiguration(step.Configuration);
            var taskConfig = config?.TaskConfig;
            if (taskConfig is not null
                && (taskConfig.RequiresDocument || string.Equals(taskConfig.TaskActionType, "document", StringComparison.OrdinalIgnoreCase)))
            {
                var name = string.IsNullOrWhiteSpace(taskConfig.DocumentName)
                    ? $"{step.Name} document"
                    : taskConfig.DocumentName.Trim();
                documents.Add(new DocumentSeed(name, step.Name, true));
            }

            foreach (var check in config?.QualityConfig?.QualityChecks ?? [])
            {
                if (!check.RequiresDocument)
                {
                    continue;
                }

                var name = string.IsNullOrWhiteSpace(check.DocumentName)
                    ? check.Name.Trim()
                    : check.DocumentName.Trim();
                documents.Add(new DocumentSeed(name, step.Name, check.IsRequired));
            }
        }

        return documents
            .Where(item => !string.IsNullOrWhiteSpace(item.Name))
            .GroupBy(item => $"{item.RequiredFrom}|{item.Name}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    private void EnsureCanEdit(ProcedureCase procedureCase)
    {
        if (procedureCase.Status == "Completed")
        {
            throw new InvalidOperationException("Completed cases cannot be changed.");
        }

        if (!CanEdit(procedureCase))
        {
            throw new UnauthorizedAccessException("The current user is not assigned to this procedure stage.");
        }
    }

    private bool CanEdit(ProcedureCase procedureCase)
    {
        if (IsWorkflowAdmin())
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(procedureCase.CurrentAssignedRole))
        {
            return true;
        }

        var assignedTokens = procedureCase.CurrentAssignedRole
            .Split(['/', ',', ';', '|'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        return _currentUser.Roles.Any(role =>
            assignedTokens.Any(token => string.Equals(token, role, StringComparison.OrdinalIgnoreCase))
            || procedureCase.CurrentAssignedRole.Contains(role, StringComparison.OrdinalIgnoreCase));
    }

    private bool IsWorkflowAdmin() =>
        _currentUser.Roles.Any(role =>
            string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, "SystemAdmin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, "SuperAdmin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, "TenantAdmin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, "WorkflowAdmin", StringComparison.OrdinalIgnoreCase));

    private Guid RequireTenantId() =>
        _currentUser.TenantId is { } tenantId && tenantId != Guid.Empty
            ? tenantId
            : throw new UnauthorizedAccessException("Tenant context is required.");

    private Guid RequireUserId() =>
        Guid.TryParse(_currentUser.UserId, out var userId) && userId != Guid.Empty
            ? userId
            : throw new UnauthorizedAccessException("User context is required.");

    private static string NormalizeModule(string module)
    {
        if (string.Equals(module, "Legal", StringComparison.OrdinalIgnoreCase))
        {
            return "Legal";
        }

        if (string.Equals(module, "Estate", StringComparison.OrdinalIgnoreCase)
            || string.Equals(module, "EstateManagement", StringComparison.OrdinalIgnoreCase)
            || string.Equals(module, "Estate Management", StringComparison.OrdinalIgnoreCase))
        {
            return "Estate";
        }

        if (string.Equals(module, "Facilities", StringComparison.OrdinalIgnoreCase))
        {
            return "Facilities";
        }

        if (string.Equals(module, "PropertyManagement", StringComparison.OrdinalIgnoreCase)
            || string.Equals(module, "Property Management", StringComparison.OrdinalIgnoreCase))
        {
            return "PropertyManagement";
        }

        if (string.Equals(module, "Planning", StringComparison.OrdinalIgnoreCase)
            || string.Equals(module, "ProjectPlanning", StringComparison.OrdinalIgnoreCase)
            || string.Equals(module, "Project Planning", StringComparison.OrdinalIgnoreCase))
        {
            return "Planning";
        }

        throw new InvalidOperationException($"Procedure module '{module}' is not supported.");
    }

    private static string? ExtractAssignedRole(string? assignmentConfiguration)
    {
        if (string.IsNullOrWhiteSpace(assignmentConfiguration))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(assignmentConfiguration);
            if (doc.RootElement.TryGetProperty("role", out var role) && role.ValueKind == JsonValueKind.String)
            {
                return role.GetString();
            }

            if (doc.RootElement.TryGetProperty("roles", out var roles) && roles.ValueKind == JsonValueKind.Array)
            {
                var roleNames = roles.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.String)
                    .Select(item => item.GetString())
                    .Where(item => !string.IsNullOrWhiteSpace(item));

                return string.Join(" / ", roleNames);
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private ProcedureCaseSummaryDto ToSummaryDto(ProcedureCase procedureCase) =>
        new(
            procedureCase.Id,
            procedureCase.Module,
            procedureCase.EntityType,
            procedureCase.Title,
            procedureCase.ReferenceNumber,
            procedureCase.ApplicantName,
            procedureCase.Status,
            procedureCase.CurrentStageIndex,
            procedureCase.CurrentStageName,
            procedureCase.CurrentAssignedRole,
            procedureCase.WorkflowDefinitionId.HasValue,
            procedureCase.WorkflowInstanceId,
            procedureCase.CreatedAt,
            procedureCase.UpdatedAt);

    private ProcedureCaseDetailDto ToDetailDto(ProcedureCase procedureCase) =>
        new(
            procedureCase.Id,
            procedureCase.Module,
            procedureCase.EntityType,
            procedureCase.Title,
            procedureCase.ReferenceNumber,
            procedureCase.ApplicantName,
            procedureCase.SourceDepartment,
            procedureCase.ReceivedDate,
            procedureCase.Description,
            procedureCase.Status,
            procedureCase.CurrentStageIndex,
            procedureCase.CurrentStageName,
            procedureCase.CurrentStageOwner,
            procedureCase.CurrentAssignedRole,
            procedureCase.WorkflowDefinitionId.HasValue,
            procedureCase.WorkflowInstanceId,
            CanEdit(procedureCase),
            procedureCase.Fields.OrderBy(item => item.CreatedAt).Select(ToFieldDto).ToList(),
            procedureCase.ChecklistItems.OrderBy(item => item.StageIndex).ThenBy(item => item.CreatedAt).Select(ToChecklistDto).ToList(),
            procedureCase.Documents.OrderBy(item => item.CreatedAt).Select(ToDocumentDto).ToList(),
            procedureCase.Activities.OrderByDescending(item => item.PerformedAt).Take(20).Select(ToActivityDto).ToList());

    private static ProcedureCaseFieldDto ToFieldDto(ProcedureCaseField field) =>
        new(field.Id, field.Key, field.Label, field.FieldType, field.Value, ParseOptions(field.OptionsJson));

    private static ProcedureCaseChecklistItemDto ToChecklistDto(ProcedureCaseChecklistItem item) =>
        new(item.Id, item.StageIndex, item.StageName, item.Text, item.IsCompleted, item.CompletedById, item.CompletedAt);

    private static ProcedureCaseDocumentDto ToDocumentDto(ProcedureCaseDocument document) =>
        new(document.Id, document.Name, document.RequiredFrom, document.IsMandatory, document.FileName, document.FileUrl, document.Notes, document.UploadedById, document.UploadedAt);

    private static ProcedureCaseActivityDto ToActivityDto(ProcedureCaseActivity activity) =>
        new(activity.Id, activity.Action, activity.StageName, activity.Details, activity.PerformedById, activity.PerformedAt);

    private static IReadOnlyList<string>? ParseOptions(string? optionsJson)
    {
        if (string.IsNullOrWhiteSpace(optionsJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<IReadOnlyList<string>>(optionsJson);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static WorkflowStepConfigurationDto? DeserializeStepConfiguration(string? configurationJson)
    {
        if (string.IsNullOrWhiteSpace(configurationJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<WorkflowStepConfigurationDto>(configurationJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static ProcedureCaseActivity Activity(Guid tenantId, Guid userId, Guid procedureCaseId, string action, string? stageName, string? details) =>
        new()
        {
            TenantId = tenantId,
            ProcedureCaseId = procedureCaseId,
            Action = action,
            StageName = stageName,
            Details = details,
            PerformedById = userId,
            CreatedById = userId,
            CreatedAt = DateTime.UtcNow,
            PerformedAt = DateTime.UtcNow
        };

    private async Task SyncCaseFromWorkflowRuntimeAsync(Guid procedureCaseId, Guid workflowInstanceId, Guid userId, string? notes = null)
    {
        var tenantId = RequireTenantId();
        var instance = await _db.WorkflowInstances
            .AsNoTracking()
            .Include(item => item.WorkflowDefinition)
                .ThenInclude(item => item.Steps)
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == workflowInstanceId && !item.IsDeleted);

        if (instance is null)
        {
            return;
        }

        var orderedSteps = instance.WorkflowDefinition.Steps
            .OrderBy(item => item.Order)
            .ThenBy(item => item.Name)
            .ToList();

        var currentStep = instance.CurrentStepId.HasValue
            ? orderedSteps.FirstOrDefault(item => item.Id == instance.CurrentStepId.Value)
            : null;

        var status = instance.Status switch
        {
            WorkflowInstanceStatus.Completed => "Completed",
            WorkflowInstanceStatus.Cancelled => "Cancelled",
            WorkflowInstanceStatus.Failed => "Failed",
            WorkflowInstanceStatus.Suspended => "Suspended",
            WorkflowInstanceStatus.Waiting => "Waiting",
            _ => "Open"
        };

        var stageIndex = currentStep is null
            ? Math.Max(0, orderedSteps.Count - 1)
            : Math.Max(0, orderedSteps.FindIndex(item => item.Id == currentStep.Id));

        var assignedRole = currentStep is null
            ? null
            : currentStep.RequiredRole ?? ExtractAssignedRole(currentStep.AssignmentConfiguration) ?? currentStep.Name;

        await _db.ProcedureCases
            .IgnoreQueryFilters()
            .Where(item => item.TenantId == tenantId && item.Id == procedureCaseId && !item.IsDeleted)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Status, status)
                .SetProperty(item => item.CurrentStageIndex, stageIndex)
                .SetProperty(item => item.CurrentStageName, currentStep != null ? currentStep.Name : "Completed")
                .SetProperty(item => item.CurrentStageOwner, currentStep != null ? currentStep.RequiredRole : null)
                .SetProperty(item => item.CurrentAssignedRole, assignedRole)
                .SetProperty(item => item.WorkflowInstanceId, workflowInstanceId)
                .SetProperty(item => item.WorkflowDefinitionId, instance.WorkflowDefinitionId)
                .SetProperty(item => item.WorkflowStepId, currentStep != null ? currentStep.Id : null)
                .SetProperty(item => item.CompletedAt, instance.Status == WorkflowInstanceStatus.Completed ? instance.CompletedDate ?? DateTime.UtcNow : null)
                .SetProperty(item => item.LastActionById, userId)
                .SetProperty(item => item.UpdatedAt, DateTime.UtcNow));

        _db.ProcedureCaseActivities.Add(Activity(tenantId, userId, procedureCaseId, "Workflow synced", currentStep?.Name ?? "Completed", notes));
        await _db.SaveChangesAsync();
    }

    private sealed record WorkspaceSeed(
        string Title,
        IReadOnlyList<StageSeed> Stages,
        IReadOnlyList<FieldSeed> Fields,
        IReadOnlyList<DocumentSeed> Documents,
        Guid? WorkflowDefinitionId,
        string? WorkflowDefinitionName);

    private sealed record StageSeed(
        int Index,
        string Name,
        string? Owner,
        string? AssignedRole,
        Guid? WorkflowStepId,
        IReadOnlyList<string> Checklist)
    {
        public Guid? WorkflowDefinitionId { get; init; }
        public string? WorkflowDefinitionName { get; init; }
    }

    private sealed record FieldSeed(string Key, string Label, string FieldType, IReadOnlyList<string>? Options);

    private sealed record DocumentSeed(string Name, string? RequiredFrom, bool IsMandatory);
}
