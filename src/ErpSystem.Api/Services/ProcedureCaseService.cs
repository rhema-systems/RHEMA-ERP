using System.Text.Json;
using ErpSystem.Api.Services.Notifications;
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
using ErpSystem.Core.Models;
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
    private readonly INotificationService _notificationService;
    private readonly IFileStorageService _fileStorageService;

    public ProcedureCaseService(
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        ILegalProcedureCatalogService legalCatalog,
        IEstateProcedureCatalogService estateCatalog,
        IFacilitiesProcedureCatalogService facilitiesCatalog,
        IPropertyManagementProcedureCatalogService propertyManagementCatalog,
        IPlanningProcedureCatalogService planningCatalog,
        IWorkflowEngine workflowEngine,
        INotificationService notificationService,
        IFileStorageService fileStorageService)
    {
        _db = db;
        _currentUser = currentUser;
        _legalCatalog = legalCatalog;
        _estateCatalog = estateCatalog;
        _facilitiesCatalog = facilitiesCatalog;
        _propertyManagementCatalog = propertyManagementCatalog;
        _planningCatalog = planningCatalog;
        _workflowEngine = workflowEngine;
        _notificationService = notificationService;
        _fileStorageService = fileStorageService;
    }

    public async Task<IReadOnlyList<ProcedureCaseSummaryDto>> GetCasesAsync(string? module, string? entityType, bool mineOnly)
    {
        var tenantId = RequireTenantId();
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

        var cases = await query
            .OrderByDescending(item => item.UpdatedAt ?? item.CreatedAt)
            .Take(IsWorkflowAdmin() ? 100 : 500)
            .ToListAsync();

        return cases
            .Where(procedureCase => CanView(procedureCase)
                && (!mineOnly || UserOwnsCase(procedureCase) || UserHasAssignedProcedureRole(procedureCase)))
            .Take(100)
            .Select(ToSummaryDto)
            .ToList();
    }

    public async Task<ProcedureCaseDetailDto?> GetCaseAsync(Guid id)
    {
        var procedureCase = await LoadCaseAsync(id, asTracking: false);
        if (procedureCase is null)
        {
            return null;
        }

        if (!CanView(procedureCase))
        {
            throw new UnauthorizedAccessException("The current user is not allowed to view this procedure case.");
        }

        return ToDetailDto(procedureCase);
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
        var isContentUrlSave = IsProcedureCaseContentUrl(request.FileUrl);
        var fileUrl = isContentUrlSave ? document.FileUrl : request.FileUrl;

        if (!isContentUrlSave && !string.IsNullOrWhiteSpace(fileUrl) && !IsPrivateProcedureDocumentPath(fileUrl))
        {
            throw new InvalidOperationException("Upload procedure case documents through the secure procedure document upload endpoint.");
        }

        await _db.ProcedureCaseDocuments
            .IgnoreQueryFilters()
            .Where(item => item.TenantId == tenantId && item.ProcedureCaseId == id && item.Id == documentId && !item.IsDeleted)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.FileName, string.IsNullOrWhiteSpace(request.FileName) ? document.FileName : request.FileName)
                .SetProperty(item => item.FileUrl, fileUrl)
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
        var completedStageName = procedureCase.CurrentStageName;

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
            var syncedCase = (await LoadCaseAsync(id, asTracking: false))!;
            await NotifyEstateProcedureHandoffsAsync(syncedCase, completedStageName, syncedCase.CurrentStageName, userId, tenantId);
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

        var updatedCase = (await LoadCaseAsync(id, asTracking: false))!;
        await NotifyEstateProcedureHandoffsAsync(updatedCase, completedStageName, nextStage?.Name, userId, tenantId);

        return ToDetailDto((await LoadCaseAsync(id, asTracking: false))!);
    }

    public async Task<ProcedureCaseDetailDto?> UploadDocumentAsync(
        Guid id,
        Guid documentId,
        Stream fileStream,
        string fileName,
        string contentType,
        long fileSize,
        string? notes)
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

        var upload = await _fileStorageService.UploadFileAsync(new FileUploadRequest
        {
            FileStream = fileStream,
            FileName = fileName,
            ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
            FileSize = fileSize,
            Category = "procedure-case-documents",
            TenantId = RequireTenantId().ToString(),
            OverwriteExisting = false
        });

        if (!upload.Success)
        {
            throw new InvalidOperationException(upload.ErrorMessage ?? "Procedure case document upload failed.");
        }

        return await AttachDocumentAsync(id, documentId, new AttachProcedureCaseDocumentRequest(
            upload.OriginalFileName,
            upload.FilePath,
            notes));
    }

    public async Task<ProcedureCaseDocumentContentDto?> GetDocumentContentAsync(Guid id, Guid documentId)
    {
        var procedureCase = await LoadCaseAsync(id, asTracking: false);
        if (procedureCase is null)
        {
            return null;
        }

        if (!CanView(procedureCase))
        {
            throw new UnauthorizedAccessException("The current user is not allowed to view this procedure case document.");
        }

        var document = procedureCase.Documents.FirstOrDefault(item => item.Id == documentId);
        if (document is null || string.IsNullOrWhiteSpace(document.FileUrl))
        {
            return null;
        }

        if (Uri.TryCreate(document.FileUrl, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException("This procedure case document is not stored in managed private storage.");
        }

        var stream = await _fileStorageService.DownloadFileAsync(document.FileUrl, document.Id);
        return new ProcedureCaseDocumentContentDto(
            stream,
            string.IsNullOrWhiteSpace(document.FileName) ? document.Name : document.FileName,
            ResolveContentType(document.FileName ?? document.Name));
    }

    private async Task NotifyEstateProcedureHandoffsAsync(
        ProcedureCase procedureCase,
        string completedStageName,
        string? nextStageName,
        Guid userId,
        Guid tenantId)
    {
        if (!string.Equals(procedureCase.Module, "Estate", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var handoffs = BuildEstateHandoffs(procedureCase, completedStageName, nextStageName);
        if (handoffs.Count == 0)
        {
            return;
        }

        foreach (var handoff in handoffs)
        {
            var sourceReference = FirstNonBlank(procedureCase.ReferenceNumber, procedureCase.Title, procedureCase.Id.ToString()) ?? procedureCase.Id.ToString();
            var sourceLabel = $"Source: Estate -> {handoff.TargetModule}";
            var actionUrl = $"/estate/{Uri.EscapeDataString(procedureCase.EntityType)}?caseId={procedureCase.Id}";
            var message = $"{procedureCase.Title} reached '{handoff.TriggerStage}'. {handoff.Reason}";

            await RoleNotificationDispatcher.NotifyRolesAsync(
                _db,
                _notificationService,
                tenantId,
                userId,
                handoff.Roles,
                $"Estate handoff to {handoff.TargetModule}",
                message,
                "estate.procedure.handoff",
                "ProcedureCase",
                procedureCase.Id,
                actionUrl,
                new Dictionary<string, object>
                {
                    ["sourceLabel"] = sourceLabel,
                    ["sourceModule"] = "Estate",
                    ["sourceEntityType"] = procedureCase.EntityType,
                    ["sourceRecordReference"] = sourceReference,
                    ["handoffTargetModule"] = handoff.TargetModule,
                    ["handoffReason"] = handoff.Reason,
                    ["completedStage"] = completedStageName,
                    ["nextStage"] = nextStageName ?? string.Empty,
                    ["applicantName"] = procedureCase.ApplicantName ?? string.Empty,
                    ["propertyNumber"] = FieldValue(procedureCase, "propertyNumber") ?? string.Empty,
                    ["financeReference"] = FieldValue(procedureCase, "financeReference") ?? string.Empty,
                    ["legalReference"] = FieldValue(procedureCase, "legalReference") ?? string.Empty,
                    ["planningReference"] = FieldValue(procedureCase, "planningReference") ?? string.Empty,
                    ["dmsFolderReference"] = FieldValue(procedureCase, "dmsFolderReference") ?? string.Empty
                },
                CancellationToken.None);

            _db.ProcedureCaseActivities.Add(Activity(
                tenantId,
                userId,
                procedureCase.Id,
                "Handoff notified",
                handoff.TriggerStage,
                $"{sourceLabel}: {handoff.Reason}"));
        }

        await _db.SaveChangesAsync();
    }

    private static IReadOnlyList<EstateProcedureHandoff> BuildEstateHandoffs(
        ProcedureCase procedureCase,
        string completedStageName,
        string? nextStageName)
    {
        var stageText = $"{completedStageName} {nextStageName}".ToLowerInvariant();
        var handoffs = new List<EstateProcedureHandoff>();

        // Handoffs are notifications plus audit entries only; target teams retain ownership of their module records.
        if (ContainsAny(stageText, "arrears", "payment", "invoice", "fee", "fees", "rent", "premium", "lmf", "ground rent", "revenue", "receipt", "debtor", "rate revision", "outstanding", "payment book"))
        {
            handoffs.Add(new(
                "Finance / Revenue",
                ["Finance Officer", "Finance Analyst", "Estate Manager"],
                "Finance must confirm receipting, arrears, invoice, statement, premium, rent, or fee outcome.",
                FirstNonBlank(nextStageName, completedStageName) ?? completedStageName));
        }

        if (ContainsAny(stageText, "legal", "deed", "assignment", "mortgage", "execution", "registered", "variation", "route legal"))
        {
            handoffs.Add(new(
                "Legal",
                ["Legal Officer", "Legal Manager", "Estate Manager"],
                "Legal must review, draft, execute, register, detach, or return the Estate instrument reference.",
                FirstNonBlank(nextStageName, completedStageName) ?? completedStageName));
        }

        if (ContainsAny(stageText, "planning", "cadastral", "site", "layout", "inspection", "development", "change-of-use", "site plan", "plot number"))
        {
            handoffs.Add(new(
                "Planning / Development",
                ["Planning Officer", "Assigned Planning Officer", "Survey Officer", "Development Officer", "Estate Manager"],
                "Planning, Development, or Survey must provide site, layout, cadastral, permit, inspection, or planning confirmation.",
                FirstNonBlank(nextStageName, completedStageName) ?? completedStageName));
        }

        if (ContainsAny(stageText, "land bank", "project management", "project handoff", "ready for project", "demarcation"))
        {
            handoffs.Add(new(
                "Project Management",
                ["Project Manager", "Estate Manager"],
                "Project Management must review the Estate land bank or project-readiness reference.",
                FirstNonBlank(nextStageName, completedStageName) ?? completedStageName));
        }

        if (procedureCase.EntityType is "EstateServicedPlotAllocation" or "EstateHousingHomeOwnership"
            && ContainsAny(stageText, "offer", "right of entry", "rent card", "update records", "close", "dispatch"))
        {
            handoffs.Add(new(
                "Estate / Property Management",
                ["Property Manager", "Property Officer", "Property Records Officer", "Estate Manager"],
                "Property Management must receive the allocation, occupancy, HOS, rent-card, or records update reference.",
                FirstNonBlank(nextStageName, completedStageName) ?? completedStageName));
        }

        if (ContainsAny(stageText, "facilities", "maintenance", "repair", "service charge", "common area"))
        {
            handoffs.Add(new(
                "Estate / Facilities and Maintenance",
                ["Facilities Manager", "Facilities Officer", "Maintenance Manager", "Maintenance Officer", "Estate Manager"],
                "Facilities or Maintenance must pick up the Estate service, maintenance, service charge, or operational reference.",
                FirstNonBlank(nextStageName, completedStageName) ?? completedStageName));
        }

        if (ContainsAny(stageText, "document", "dms", "dispatch", "offer", "right of entry", "proposal", "certified", "search report", "notice", "letter", "rent card", "report", "records", "ledger", "register"))
        {
            handoffs.Add(new(
                "Central DMS / Records",
                ["Records Officer", "Land Registry Officer", "Document Control Officer", "Estate Officer", "Estate Manager"],
                "Central DMS or Estate Records must index, version, dispatch, annotate, or retain the generated Estate document reference.",
                FirstNonBlank(nextStageName, completedStageName) ?? completedStageName));
        }

        return handoffs
            .GroupBy(item => item.TargetModule, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    private static bool ContainsAny(string value, params string[] tokens)
        => tokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase));

    private static string? FieldValue(ProcedureCase procedureCase, string key) =>
        procedureCase.Fields.FirstOrDefault(field =>
            string.Equals(field.Key, key, StringComparison.OrdinalIgnoreCase))?.Value;

    private static string? FirstNonBlank(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

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

    private static IReadOnlyList<FieldSeed> BuildEstateFieldSeeds(EstateProcedureCatalogItem procedure)
    {
        var fields = new List<FieldSeed>
        {
            new("referenceNumber", "Reference number", "text", null),
            new("procedureType", "Procedure", "text", [procedure.Title]),
            new("applicantName", "Applicant / lessee / client name", "text", null),
            new("propertyNumber", "Property / plot / house number", "text", null),
            new("fileReference", "Estate file reference", "text", null),
            new("schedule", "Estate schedule", "select", ["Registry", "Records", "Serviced Plots", "Lands / Partially Serviced", "Housing", "Traditional Lands", "Regularisation", "Facilities"]),
            new("location", "Location / community", "text", null),
            new("sourceDepartment", "Source department", "text", null),
            new("sourceLabel", "Source label", "text", null),
            new("sourceSystem", "Source system", "text", null),
            new("sourceWorkspace", "Source workspace", "text", null),
            new("receivedDate", "Received date", "date", null),
            new("assignedOfficer", "Assigned Estate officer", "text", null),
            new("arrearsStatus", "Arrears status", "select", ["Not checked", "No arrears", "Arrears exist", "Waiver / exception approved"]),
            new("feeReference", "Fee / invoice / receipt reference", "text", null),
            new("legalReference", "Legal reference", "text", null),
            new("financeReference", "Finance reference", "text", null),
            new("planningReference", "Planning / site plan reference", "text", null),
            new("dmsFolderReference", "DMS folder reference", "text", null)
        };

        // Estate manual controls stay in Estate; linked teams receive source/reference fields without changing their modules.
        if (string.Equals(procedure.EntityType, "EstateRegistrySecretariat", StringComparison.OrdinalIgnoreCase))
        {
            fields.AddRange([
                new("intakeType", "Intake type", "select", ["Incoming file", "Outgoing letter", "Form purchase", "Typing request", "Client pickup", "Internal dispatch"]),
                new("registryBook", "Registry book", "select", ["General notebook", "Regularization notebook", "Kpone notebook", "Letters book", "Forms purchase book", "Movement register"]),
                new("formType", "Form type", "select", ["Estate Transfer Form", "House Ownership Scheme Form", "Rental Unit Form", "Other"]),
                new("receiptNumber", "Receipt number", "text", null),
                new("fileComingFrom", "File coming from", "text", null),
                new("referredOfficer", "Referred officer", "text", null),
                new("typingOutputType", "Typing / letter output type", "select", ["Notification letter", "Offer Letter", "Right of Entry", "Demand Letter", "Rate Revision Letter", "Lease Request", "Invoice", "Other"]),
                new("dispatchDate", "Dispatch date", "date", null),
                new("clientUpdate", "Client update", "textarea", null)
            ]);
        }

        if (string.Equals(procedure.EntityType, "EstateRecordsManagement", StringComparison.OrdinalIgnoreCase))
        {
            fields.AddRange([
                new("recordActionType", "Record action type", "select", ["Estate register update", "HOS ledger update", "Rent register update", "Transfer amendment", "Agency notification", "Building permit ownership verification", "Invitation / mediation letter"]),
                new("registerReference", "Register / ledger reference", "text", null),
                new("oldLesseeName", "Previous lessee / tenant name", "text", null),
                new("newLesseeName", "New lessee / tenant name", "text", null),
                new("addressOnRecord", "Address on record", "text", null),
                new("buildingPermitReference", "Building permit reference", "text", null),
                new("ownershipVerificationStatus", "Ownership verification status", "select", ["Not checked", "Matches records", "Mismatch found", "Returned for correction"]),
                new("indebtednessStatus", "Indebtedness status", "select", ["Not checked", "No indebtedness", "Indebted", "Revenue confirmation pending"]),
                new("agencyNotifications", "Agency notifications", "textarea", null)
            ]);
        }

        if (string.Equals(procedure.EntityType, "EstateInspection", StringComparison.OrdinalIgnoreCase))
        {
            fields.AddRange([
                new("inspectionType", "Inspection type", "select", ["Site report", "Substantial development check", "Tenancy compliance", "Boundary / neighbourhood check", "Encroachment / non-compliance", "Handover / return"]),
                new("inspectionDate", "Inspection date", "date", null),
                new("inspectionOfficer", "Inspection officer", "text", null),
                new("neighbourhoodDetails", "Neighbourhood details", "textarea", null),
                new("developmentStatus", "Development status", "select", ["Not checked", "Vacant", "Undeveloped", "Partially developed", "Substantially developed", "Completed", "Occupied", "Encroached"]),
                new("complianceObservation", "Compliance observation", "textarea", null),
                new("photoEvidenceReference", "Photo evidence reference", "text", null)
            ]);
        }

        if (string.Equals(procedure.EntityType, "EstateSearchApplication", StringComparison.OrdinalIgnoreCase))
        {
            fields.AddRange([
                new("searchPurpose", "Search purpose", "text", null),
                new("searchPeriod", "Search period / scope", "text", null),
                new("searchFeeReceipt", "Search fee receipt", "text", null),
                new("searchReportReference", "Search report reference", "text", null)
            ]);
        }

        if (string.Equals(procedure.EntityType, "EstateCertifiedTrueCopy", StringComparison.OrdinalIgnoreCase))
        {
            fields.AddRange([
                new("documentToCertify", "Document to certify", "select", ["Offer Letter", "Right of Entry", "Lease", "Rent Card", "Allocation Letter", "Other"]),
                new("originalDocumentReference", "Original document reference", "text", null),
                new("certificationFeeReceipt", "Certification fee receipt", "text", null),
                new("certificationDate", "Certification date", "date", null)
            ]);
        }

        // Estate manual sections 5.3 and 7.3 require Estate to calculate LMF and Ground Rent before Finance receipting.
        if (HasLandFeeDetermination(procedure.EntityType))
        {
            fields.AddRange([
                new("landUse", "Land use", "select", ["Residential", "Commercial", "Institutional", "Industrial", "Agro Industrial", "Fuel Station", "Mixed Use", "Other"]),
                new("plotSizeAcres", "Plot size (acres)", "number", null),
                new("plotSizeHectares", "Plot size (hectares)", "number", null),
                new("lmfRatePerAcre", "LMF rate per acre", "currency", null),
                new("landManagementFeePayable", "Land Management Fee payable", "currency", null),
                new("groundRentRatePerAcre", "Ground Rent rate per acre", "currency", null),
                new("groundRentComputed", "Ground Rent computed", "currency", null),
                new("groundRentPayable", "Ground Rent payable", "currency", null),
                new("paymentDeadline", "Payment deadline", "date", null),
                new("offerExpiryDate", "Offer expiry date", "date", null),
                new("leaseTermYears", "Lease term (years)", "number", null),
                new("dateOfTenancy", "Date of tenancy", "date", null),
                new("acceptanceDate", "Acceptance date", "date", null),
                new("proposalLetterReference", "Proposal letter reference", "text", null),
                new("offerLetterReference", "Offer letter reference", "text", null),
                new("rightOfEntryReference", "Right of Entry reference", "text", null)
            ]);
        }

        if (string.Equals(procedure.EntityType, "EstateTransfer", StringComparison.OrdinalIgnoreCase)
            || string.Equals(procedure.EntityType, "EstateAssignment", StringComparison.OrdinalIgnoreCase)
            || string.Equals(procedure.EntityType, "EstateJointOwnership", StringComparison.OrdinalIgnoreCase))
        {
            fields.AddRange([
                new("transferorName", "Transferor / assignor / existing lessee", "text", null),
                new("transfereeName", "Transferee / assignee / incoming party", "text", null),
                new("considerationAmount", "Consideration amount", "currency", null),
                new("transferFeePayable", "Transfer / assignment fee payable", "currency", null),
                new("executionStatus", "Execution status", "select", ["Not started", "Prepared", "Signed", "Registered", "Records updated"])
            ]);
        }

        if (string.Equals(procedure.EntityType, "EstateMortgageConsent", StringComparison.OrdinalIgnoreCase))
        {
            fields.AddRange([
                new("mortgageConsentType", "Mortgage consent type", "select", ["Consent to Mortgage", "Mortgage in Principle"]),
                new("mortgageeName", "Mortgagee / financial institution", "text", null),
                new("draftDeedReference", "Draft deed / mortgage document reference", "text", null),
                new("developmentStatus", "Development status", "select", ["Not checked", "Undeveloped", "Partially developed", "Substantially developed", "Completed"]),
                new("consentDecision", "Consent decision", "select", ["Pending", "Approved", "Returned", "Rejected"])
            ]);
        }

        if (string.Equals(procedure.EntityType, "EstateLeasePreparation", StringComparison.OrdinalIgnoreCase))
        {
            fields.AddRange([
                new("developmentStatus", "Development status", "select", ["Not checked", "Undeveloped", "Partially developed", "Substantially developed", "Completed"]),
                new("buildingPermitReference", "Building permit reference", "text", null),
                new("leasePreparationFee", "Lease preparation fee", "currency", null),
                new("cadastralInvoiceReference", "Cadastral invoice reference", "text", null),
                new("leaseRequestFormReference", "Lease request form reference", "text", null),
                new("registeredLeaseReference", "Registered lease reference", "text", null)
            ]);
        }

        if (string.Equals(procedure.EntityType, "EstateLeaseRenewal", StringComparison.OrdinalIgnoreCase))
        {
            fields.AddRange([
                new("existingLeaseExpiryDate", "Existing lease expiry date", "date", null),
                new("yearsToExpiry", "Years to expiry", "number", null),
                new("developmentStatus", "Development proposal / status", "text", null),
                new("renewalPremium", "Renewal premium", "currency", null),
                new("improvedGroundRent", "Improved Ground Rent", "currency", null),
                new("committeeDecision", "LRTC decision", "select", ["Pending", "Approved", "Returned", "Rejected"])
            ]);
        }

        if (string.Equals(procedure.EntityType, "EstateAdditionalLand", StringComparison.OrdinalIgnoreCase))
        {
            fields.AddRange([
                new("adjoiningPlotNumber", "Adjoining plot number", "text", null),
                new("additionalLandSizeAcres", "Additional land size (acres)", "number", null),
                new("availabilityStatus", "Availability status", "select", ["Not checked", "Available", "Unavailable", "Disputed", "Requires layout revision"]),
                new("recommendation", "Estate recommendation", "textarea", null),
                new("approvalDecision", "Approval decision", "select", ["Pending", "Approved", "Returned", "Rejected"])
            ]);
        }

        if (string.Equals(procedure.EntityType, "EstateLayoutRevision", StringComparison.OrdinalIgnoreCase))
        {
            fields.AddRange([
                new("existingLayoutReference", "Existing layout reference", "text", null),
                new("proposedLayoutReference", "Proposed layout reference", "text", null),
                new("revisionReason", "Revision reason", "textarea", null),
                new("planningComment", "Planning comment", "textarea", null),
                new("mdApprovalReference", "MD approval reference", "text", null)
            ]);
        }

        if (string.Equals(procedure.EntityType, "EstateChangeOfUse", StringComparison.OrdinalIgnoreCase))
        {
            fields.AddRange([
                new("existingUse", "Existing use", "select", ["Residential", "Commercial", "Institutional", "Industrial", "Agro Industrial", "Fuel Station", "Mixed Use", "Other"]),
                new("newUse", "New use", "select", ["Residential", "Commercial", "Institutional", "Industrial", "Agro Industrial", "Fuel Station", "Mixed Use", "Other"]),
                new("plotSizeAcres", "Plot size (acres)", "number", null),
                new("existingLmfRatePerAcre", "Existing-use LMF rate per acre", "currency", null),
                new("newLmfRatePerAcre", "New-use LMF rate per acre", "currency", null),
                new("changeOfUseFeePayable", "Change-of-use fee payable", "currency", null),
                new("groundRentLossPresentValue", "Present value of Ground Rent loss", "currency", null),
                new("administrativeFeePayable", "Administrative fee payable", "currency", null),
                new("newGroundRentRatePerAcre", "New Ground Rent rate per acre", "currency", null),
                new("newGroundRentPayable", "New Ground Rent payable", "currency", null),
                new("changeOfUseDecision", "Change-of-use decision", "select", ["Pending", "Approved", "Returned", "Rejected"])
            ]);
        }

        if (string.Equals(procedure.EntityType, "EstateReminderRateRevision", StringComparison.OrdinalIgnoreCase))
        {
            fields.AddRange([
                new("noticeType", "Notice type", "select", ["Proposal reminder", "Rate revision notice", "Ground rent arrears demand"]),
                new("originalProposalReference", "Original proposal reference", "text", null),
                new("outstandingAmount", "Outstanding amount", "currency", null),
                new("revisedAmountPayable", "Revised amount payable", "currency", null),
                new("noticeDate", "Notice date", "date", null),
                new("dispatchReference", "Dispatch reference", "text", null)
            ]);
        }

        if (string.Equals(procedure.EntityType, "EstateRecordAmendment", StringComparison.OrdinalIgnoreCase))
        {
            fields.AddRange([
                new("oldAddress", "Previous address", "text", null),
                new("newAddress", "New address", "text", null),
                new("declarationReference", "Statutory declaration reference", "text", null)
            ]);
        }

        if (string.Equals(procedure.EntityType, "EstateTenancyRegularisation", StringComparison.OrdinalIgnoreCase))
        {
            fields.AddRange([
                new("regularisationApproach", "Regularisation approach", "select", ["Direct approach", "Indirect approach"]),
                new("planLayoutStatus", "Planning layout status", "select", ["Not checked", "Satisfies approved layout", "Requires planning review", "Rejected"]),
                new("invitationLetterReference", "Invitation letter reference", "text", null),
                new("interviewDate", "Interview date", "date", null)
            ]);
        }

        if (string.Equals(procedure.EntityType, "EstateServicedPlotAllocation", StringComparison.OrdinalIgnoreCase))
        {
            fields.AddRange([
                new("applicationFormReference", "Application form reference", "text", null),
                new("allocationReference", "Allocation reference", "text", null),
                new("depositReceiptNumber", "Deposit receipt number", "text", null),
                new("paymentConfirmationReference", "Payment confirmation reference", "text", null),
                new("allocationType", "Allocation type", "select", ["Serviced plot", "HOS unit", "Substitution", "Reallocation"]),
                new("allocationDecision", "Allocation decision", "select", ["Pending", "Approved", "Returned", "Rejected"]),
                new("paymentBookReference", "Payment book reference", "text", null),
                new("offerLetterReference", "Offer Letter reference", "text", null),
                new("acceptanceDate", "Acceptance date", "date", null),
                new("rightOfEntryIssuedDate", "Right of Entry issued date", "date", null)
            ]);
        }

        if (string.Equals(procedure.EntityType, "EstateHousingHomeOwnership", StringComparison.OrdinalIgnoreCase))
        {
            fields.AddRange([
                new("housingRequestType", "Housing request type", "select", ["Recognition of tenancy", "Rental transfer", "Conversion to HOS", "Purchase completion", "Rental offer", "Lease request"]),
                new("houseType", "House type", "text", null),
                new("unitNumber", "Unit / house number", "text", null),
                new("declarationReference", "Statutory declaration reference", "text", null),
                new("rentCardNumber", "Rent card number", "text", null),
                new("rentRegisterReference", "Rent register reference", "text", null),
                new("sellingPrice", "Selling price", "currency", null),
                new("paymentCompletionStatus", "Payment completion status", "select", ["Not checked", "Deposit paid", "Arrears cleared", "Full selling price paid", "Payment incomplete"]),
                new("dateOfTenancy", "Date of tenancy", "date", null)
            ]);
        }

        if (string.Equals(procedure.EntityType, "EstateTraditionalLands", StringComparison.OrdinalIgnoreCase))
        {
            fields.AddRange([
                new("traditionalCouncil", "Traditional Council / Stool", "select", ["Tema Manhean", "Nungua", "Kpone", "Other"]),
                new("allocationLetterReference", "Traditional allocation letter reference", "text", null),
                new("sitePlanReference", "Traditional site plan reference", "text", null),
                new("priorAllocationStatus", "Prior allocation status", "select", ["Not checked", "No prior allocation", "Prior allocation found", "Disputed", "Undefined signatories"]),
                new("rejectionReason", "Rejection reason", "textarea", null)
            ]);
        }

        if (string.Equals(procedure.EntityType, "EstateReportingControls", StringComparison.OrdinalIgnoreCase))
        {
            fields.AddRange([
                new("reportType", "Report type", "select", ["Quarterly productivity", "Rent roll", "Debtor list", "Allocation report", "Transfer and assignment report", "Lease and mortgage report", "Control exception register", "Board summary"]),
                new("reportingPeriod", "Reporting period", "text", null),
                new("applicationsReceived", "Applications received", "number", null),
                new("applicationsProcessed", "Applications processed", "number", null),
                new("expectedRevenue", "Expected revenue", "currency", null),
                new("paymentsReceived", "Payments received", "currency", null),
                new("debtorCount", "Debtor count", "number", null),
                new("exceptionSummary", "Exception summary", "textarea", null),
                new("reportRecipient", "Report recipient", "text", null)
            ]);
        }

        return fields;
    }

    private static IReadOnlyList<DocumentSeed> BuildEstateDocumentSeeds(EstateProcedureCatalogItem procedure)
    {
        var documents = new List<DocumentSeed>
        {
            new("Application letter / request form", "Applicant / Registry", true),
            new("Property file extract", "Estate Registry / Records", true),
            new("Ownership, tenancy, lease, or allocation evidence", "Applicant / Estate Records", true),
            new("Revenue / arrears / payment confirmation", "Finance / Revenue", false),
            new("Site plan, cadastral plan, layout, or inspection evidence", "Planning / Development / Estate", false),
            new("Approval, recommendation, or routing note", "HOE / EM / EO", true),
            new($"{procedure.Title} output", "Estate Department", true),
            new("Central DMS reference", "Document Mngt", false)
        };

        // These document seeds mirror Estate-owned procedure evidence; linked teams keep ownership of their source modules.
        documents.AddRange(procedure.EntityType switch
        {
            "EstateRegistrySecretariat" => [
                new("Incoming notebook / registry entry", "Estate Registry", true),
                new("Forms purchase receipt", "Estate Registry / Revenue", false),
                new("Letters book or dispatch entry", "Estate Registry", true),
                new("File movement trace", "Estate Registry", true),
                new("Typed letter or notice", "Estate Registry", false)
            ],
            "EstateRecordsManagement" => [
                new("Estate register or ledger extract", "Estate Records", true),
                new("Revenue and Development consistency check", "Estate Records / Revenue / Development", true),
                new("Building permit ownership verification form", "Development / Estate Records", false),
                new("Agency notification letter", "Estate Records", false),
                new("Records amendment evidence", "Estate Records", true)
            ],
            "EstateInspection" => [
                new("Inspection request", "Estate / Linked Department", true),
                new("Site inspection report", "Estate Inspection", true),
                new("Photo evidence", "Estate Inspection", false),
                new("Compliance or development observation", "Estate Inspection", true)
            ],
            "EstateSearchApplication" => [
                new("Search application form", "Applicant / Registry", true),
                new("Search fee receipt", "Revenue", false),
                new("Search report", "Estate Records", true)
            ],
            "EstateCertifiedTrueCopy" => [
                new("Certified copy application", "Applicant / Registry", true),
                new("Certification fee receipt", "Revenue", false),
                new("Certified True Copy draft", "Estate Records", true),
                new("Certification approval", "HOE", true)
            ],
            "EstateLeasePreparation" => [
                new("Building permit confirmation", "Building Inspectorate / Development", true),
                new("Substantial development site report", "Estate / Development", true),
                new("Lease request form to Legal", "Estate / Legal", true),
                new("Registered lease copy for detachment", "Legal / Lands Commission", false)
            ],
            "EstateMortgageConsent" => [
                new("Consent to mortgage / mortgage in principle application", "Applicant", true),
                new("Draft deed or mortgage document", "Applicant / Legal", true),
                new("Development and arrears verification", "Estate / Finance Revenue", true),
                new("Mortgage consent response", "Estate Department", true)
            ],
            "EstateAdditionalLand" => [
                new("Additional land application", "Applicant", true),
                new("Adjoining plot verification", "Estate Records", true),
                new("Inspection and availability report", "Estate / Planning", true),
                new("Approval recommendation", "HOE / MD", true),
                new("Offer or refusal letter", "Estate Department", true)
            ],
            "EstateLayoutRevision" => [
                new("Layout revision request", "Applicant / Estate", true),
                new("Existing and proposed layout plans", "Planning / Development", true),
                new("Layout revision letter", "Estate Department", true),
                new("MD signed approval", "MD / HOE", true)
            ],
            "EstateChangeOfUse" => [
                new("Change-of-use application", "Applicant", true),
                new("Site inspection report", "Estate / Planning", true),
                new("Change-of-use fee calculation", "Estate Department", true),
                new("Approval or refusal letter", "HOE / MD", true),
                new("Payment confirmation", "Finance / Revenue", false)
            ],
            "EstateReminderRateRevision" => [
                new("Unpaid proposal / arrears schedule", "Estate / Revenue", true),
                new("Reminder or rate revision notice", "Estate Department", true),
                new("Signed notice approval", "HOE / MD", true),
                new("Dispatch evidence", "Registry", true)
            ],
            "EstateLeaseRenewal" => [
                new("Lease renewal application", "Applicant", true),
                new("Lease Renewal Technical Committee approval", "LRTC", true),
                new("Renewal invoice / demand letter", "Estate / Finance Revenue", true),
                new("Deed of Variation draft", "Estate / Legal", true)
            ],
            "EstateServicedPlotAllocation" => [
                new("Completed application form", "Applicant / Marketing", true),
                new("Deposit receipt", "Revenue / Marketing", true),
                new("Allocation approval", "MD / Estate", true),
                new("Offer Letter", "Estate Serviced Plots", true),
                new("Right of Entry", "Estate Serviced Plots", true),
                new("Payment book update evidence", "Revenue / Estate Records", true)
            ],
            "EstateLandsPartiallyServiced" => [
                new("Proposal Letter with LMF and Ground Rent", "Estate Lands / Partially Serviced", true),
                new("Offer Letter", "Estate Lands / Partially Serviced", true),
                new("Right of Entry", "Estate Lands / Partially Serviced", true)
            ],
            "EstateHousingHomeOwnership" => [
                new("Recognition or HOS application", "Applicant / Housing", true),
                new("Rental Transfer Form", "Housing Section", false),
                new("Rent Card", "Housing / MD", false),
                new("HOS Offer Letter", "Housing Section", true),
                new("Payment completion evidence", "Revenue / Housing", false),
                new("Lease request for purchased house", "Housing / Legal", false)
            ],
            "EstateTraditionalLands" => [
                new("Traditional Council allocation letter", "Traditional Council", true),
                new("Traditional Council site plan", "Traditional Council / Planning", true),
                new("Proposal Letter with LMF and Ground Rent", "Estate Traditional Lands", true),
                new("Offer Letter", "Estate Traditional Lands", true),
                new("Right of Entry", "Estate Traditional Lands", true)
            ],
            "EstateTenancyRegularisation" => [
                new("Invitation letter", "Estate Regularisation", true),
                new("Regularisation requirements pack", "Applicant", true),
                new("Planning plot-number confirmation", "Planning Section", true),
                new("Committee vetting approval", "Estate Regularisation Committee", true),
                new("Proposal Letter with LMF and Ground Rent", "Estate Regularisation", true),
                new("Offer Letter", "Estate Regularisation", true),
                new("Right of Entry", "Estate Regularisation", true)
            ],
            "EstateReportingControls" => [
                new("Quarterly productivity report", "Estate Schedules", true),
                new("Rent roll", "Housing / Estate Records", false),
                new("Debtor list", "Revenue / Estate", false),
                new("Allocation and expected revenue report", "Estate Schedules", true),
                new("Control exception register", "Estate Management", true),
                new("Approved report pack", "HOE / Estate Managers", true)
            ],
            _ => []
        });

        return documents
            .GroupBy(item => $"{item.RequiredFrom}|{item.Name}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    private static bool HasLandFeeDetermination(string entityType) =>
        string.Equals(entityType, "EstateLandsPartiallyServiced", StringComparison.OrdinalIgnoreCase)
        || string.Equals(entityType, "EstateTraditionalLands", StringComparison.OrdinalIgnoreCase)
        || string.Equals(entityType, "EstateTenancyRegularisation", StringComparison.OrdinalIgnoreCase);

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
            "EstateMortgageConsent" => ["Receive mortgage consent request", "Verify arrears and development status", "Review draft deed or mortgage in principle", "Approve consent instruction", "Route Legal if required", "Dispatch consent response"],
            "EstateLeasePreparation" => ["Receive lease request", "Verify development and property status", "Confirm cadastral requirements", "Prepare invoice instruction", "Confirm payment", "Route Legal preparation", "Update records", "Dispatch lease"],
            "EstateAdditionalLand" => ["Receive additional land application", "Verify adjoining property records", "Conduct inspection and availability check", "Calculate fees and prepare recommendation", "Approve application", "Prepare offer and update records"],
            "EstateLayoutRevision" => ["Receive layout revision request", "Review planning and site implications", "Prepare layout revision letter", "Approve and sign revision", "Dispatch and update records"],
            "EstateChangeOfUse" => ["Receive change-of-use application", "Verify current use and arrears", "Inspect site and review planning", "Calculate change-of-use fee", "Approve change-of-use request", "Dispatch decision and update records"],
            "EstateReminderRateRevision" => ["Identify unpaid proposal or arrears cases", "Validate revised rates and balances", "Prepare reminder or rate revision notice", "Approve and sign notice", "Dispatch and record follow-up"],
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

        return UserHasAssignedProcedureRole(procedureCase);
    }

    private bool CanView(ProcedureCase procedureCase)
        => IsWorkflowAdmin() || UserOwnsCase(procedureCase) || UserHasAssignedProcedureRole(procedureCase);

    private bool UserOwnsCase(ProcedureCase procedureCase)
        => Guid.TryParse(_currentUser.UserId, out var userId) && procedureCase.OpenedById == userId;

    private bool UserHasAssignedProcedureRole(ProcedureCase procedureCase)
    {
        if (string.IsNullOrWhiteSpace(procedureCase.CurrentAssignedRole))
        {
            return false;
        }

        var assignedTokens = SplitAssignedRoles(procedureCase.CurrentAssignedRole);

        return _currentUser.Roles.Any(role =>
            assignedTokens.Any(token => string.Equals(token, role, StringComparison.OrdinalIgnoreCase)));
    }

    private static string[] SplitAssignedRoles(string assignedRole)
        => assignedRole
            .Split(['/', ',', ';', '|'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    private static bool IsPrivateProcedureDocumentPath(string fileUrl)
        => fileUrl.Replace('\\', '/').TrimStart('/')
            .StartsWith("private/procedure-case-documents/", StringComparison.OrdinalIgnoreCase);

    private static bool IsProcedureCaseContentUrl(string? fileUrl)
        => !string.IsNullOrWhiteSpace(fileUrl)
            && fileUrl.Replace('\\', '/').TrimStart('/')
                .StartsWith("api/procedure-cases/", StringComparison.OrdinalIgnoreCase);

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
        new(
            document.Id,
            document.Name,
            document.RequiredFrom,
            document.IsMandatory,
            document.FileName,
            string.IsNullOrWhiteSpace(document.FileUrl)
                ? null
                : $"/api/procedure-cases/{document.ProcedureCaseId}/documents/{document.Id}/content",
            document.Notes,
            document.UploadedById,
            document.UploadedAt);

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

    private static string ResolveContentType(string fileName)
        => Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".txt" => "text/plain",
            _ => "application/octet-stream"
        };

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

    private sealed record EstateProcedureHandoff(
        string TargetModule,
        IReadOnlyList<string> Roles,
        string Reason,
        string TriggerStage);
}
