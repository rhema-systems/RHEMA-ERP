using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using ErpSystem.Api.Services.DocumentManagement;
using ErpSystem.Api.Services.Notifications;
using ErpSystem.Core.DTOs.AR;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.DTOs.Notifications;
using ErpSystem.Core.DTOs.Procedures;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procedures;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Estate;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Legal;
using ErpSystem.Core.Interfaces.Maintenance;
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
    private static readonly (string Name, string StageName, string Provider)[] LegalTransferDocumentStages =
    [
        ("Transfer file from Estate", "Head of Legal Minuting", "Estate / Property Management"),
        ("Transfer fee payment receipt", "Client Payment Call", "Finance / Client"),
        ("Draft transfer form", "Transfer Drafting", "Legal Admin Assistant"),
        ("Executed transfer form", "Client Execution", "Client / Legal"),
        ("Signed transfer distribution / Estate return note", "Legal Admin Closeout", "Legal Admin Assistant")
    ];

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
    private readonly IInvoiceService _invoiceService;
    private readonly ICentralDocumentPdfSigningService _pdfSigningService;
    private readonly IJobCardService _jobCardService;
    private IReadOnlyCollection<string>? _currentUserRoleNames;

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
        IFileStorageService fileStorageService,
        IInvoiceService invoiceService,
        ICentralDocumentPdfSigningService pdfSigningService,
        IJobCardService jobCardService)
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
        _invoiceService = invoiceService;
        _pdfSigningService = pdfSigningService;
        _jobCardService = jobCardService;
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

        var visibleCases = await LoadVisibleProcedureCasesAsync(query, mineOnly, 100);

        return visibleCases
            .Select(ToSummaryDto)
            .ToList();
    }

    public async Task<IReadOnlyList<ProcedureCaseSubmissionDocumentRequirementDto>> GetSubmissionDocumentRequirementsAsync(
        string module,
        string entityType)
    {
        var workspace = await BuildWorkspaceSeedAsync(NormalizeModule(module), entityType.Trim());
        var firstStageName = workspace.Stages.OrderBy(stage => stage.Index).FirstOrDefault()?.Name;
        return workspace.Documents
            .Where(document => string.Equals(document.RequiredFrom, firstStageName, StringComparison.OrdinalIgnoreCase)
                && string.Equals(ResolveDocumentProvider(document.Name, document.ProvidedBy), "Customer", StringComparison.OrdinalIgnoreCase))
            .Select(document => new ProcedureCaseSubmissionDocumentRequirementDto(
                document.Name,
                document.DocumentType,
                NormalizeDocumentApplicability(document.AppliesTo),
                document.IsMandatory))
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

        return await ToDetailDtoAsync(procedureCase);
    }

    private async Task<List<ProcedureCase>> LoadVisibleProcedureCasesAsync(
        IQueryable<ProcedureCase> query,
        bool mineOnly,
        int take)
    {
        const int BatchSize = 200;
        var visibleCases = new List<ProcedureCase>();
        var offset = 0;

        while (visibleCases.Count < take)
        {
            var batch = await query
                .OrderByDescending(item => item.UpdatedAt ?? item.CreatedAt)
                .Skip(offset)
                .Take(BatchSize)
                .ToListAsync();

            if (batch.Count == 0)
            {
                break;
            }

            foreach (var procedureCase in batch)
            {
                if (CanView(procedureCase)
                    && (!mineOnly || UserOwnsCase(procedureCase) || UserHasAssignedProcedureRole(procedureCase)))
                {
                    visibleCases.Add(procedureCase);
                    if (visibleCases.Count == take)
                    {
                        break;
                    }
                }
            }

            offset += batch.Count;
        }

        return visibleCases;
    }

    public async Task<ProcedureCaseDetailDto> CreateCaseAsync(CreateProcedureCaseRequest request)
        => await CreateCaseCoreAsync(request, enforceLegalCreatePermission: true);

    private async Task<ProcedureCaseDetailDto> CreateCaseCoreAsync(
        CreateProcedureCaseRequest request,
        bool enforceLegalCreatePermission)
    {
        var tenantId = RequireTenantId();
        var userId = RequireUserId();
        var now = DateTime.UtcNow;
        var module = NormalizeModule(request.Module);
        var entityType = request.EntityType.Trim();
        if (enforceLegalCreatePermission)
        {
            EnsureCanCreateProcedureCase(module);
        }
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

        var requestApplicability = ResolveRequestApplicability(request.FieldValues);
        foreach (var document in workspace.Documents.Where(document =>
                     DocumentAppliesToRequest(document.AppliesTo, requestApplicability)))
        {
            procedureCase.Documents.Add(new ProcedureCaseDocument
            {
                TenantId = tenantId,
                Name = document.Name,
                RequiredFrom = document.RequiredFrom,
                ProvidedBy = ResolveDocumentProvider(document.Name, document.ProvidedBy),
                IsMandatory = document.IsMandatory,
                CreatedById = userId,
                CreatedAt = now
            });
        }

        procedureCase.Activities.Add(Activity(tenantId, userId, procedureCase.Id, "Created", firstStage.Name, "Procedure case opened."));

        // Procedure cases are source records for workflow; keep creation, workflow startup, and linkage atomic.
        // SQL Server retrying execution strategies require user-initiated transactions to run inside the
        // strategy delegate so the complete transaction can be retried as one unit.
        var executionStrategy = _db.Database.CreateExecutionStrategy();
        await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync();
            _db.ProcedureCases.Add(procedureCase);
            await _db.SaveChangesAsync();

            if (workspace.WorkflowDefinitionId is { } workflowDefinitionId)
            {
                var workflowInstance = await _workflowEngine.StartWorkflowAsync(
                    workflowDefinitionId,
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

                // The workflow engine persists through repositories that share this scoped DbContext.
                // Clear their completed tracking graph before linking the source case so stale workflow
                // entities cannot be written a second time as part of the case update.
                _db.ChangeTracker.Clear();
                var linkedRows = await _db.ProcedureCases
                    .IgnoreQueryFilters()
                    .Where(item =>
                        item.TenantId == tenantId &&
                        item.Id == procedureCase.Id &&
                        !item.IsDeleted)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(item => item.WorkflowInstanceId, workflowInstance.Id)
                        .SetProperty(item => item.WorkflowDefinitionId, workflowInstance.WorkflowDefinitionId)
                        .SetProperty(item => item.WorkflowStepId, workflowInstance.CurrentStepId)
                        .SetProperty(item => item.UpdatedAt, DateTime.UtcNow));
                if (linkedRows != 1)
                {
                    throw new InvalidOperationException("The procedure case could not be linked to its workflow instance.");
                }

                _db.ProcedureCaseActivities.Add(Activity(
                    tenantId,
                    userId,
                    procedureCase.Id,
                    "Workflow started",
                    firstStage.Name,
                    workspace.WorkflowDefinitionName));
                await _db.SaveChangesAsync();
                await SyncCaseFromWorkflowRuntimeAsync(procedureCase.Id, workflowInstance.Id, userId);
            }

            await transaction.CommitAsync();
        });

        return await ToDetailDtoAsync((await LoadCaseAsync(procedureCase.Id, asTracking: false))!);
    }

    public async Task<ProcedureCaseDetailDto> CreateLinkedLegalMatterAsync(
        Guid sourceProcedureCaseId,
        CreateLinkedLegalMatterRequest request)
    {
        var tenantId = RequireTenantId();
        var userId = RequireUserId();
        var sourceCase = await _db.ProcedureCases
            .Include(item => item.Fields.Where(field => !field.IsDeleted))
            .Include(item => item.Documents.Where(document => !document.IsDeleted))
            .FirstOrDefaultAsync(item => item.TenantId == tenantId
                && item.Id == sourceProcedureCaseId
                && !item.IsDeleted);
        if (sourceCase is null)
        {
            throw new InvalidOperationException("The originating property case was not found.");
        }

        if (!IsPropertyManagementCase(sourceCase) || !CanInitiatePropertyLegalHandoff(sourceCase))
        {
            throw new UnauthorizedAccessException("The current user cannot lodge this property matter with Legal.");
        }

        var matter = ResolveLinkedLegalMatter(request.MatterType);
        var existing = await _db.ProcedureCases
            .Include(item => item.Fields.Where(field => !field.IsDeleted))
            .Include(item => item.Documents.Where(document => !document.IsDeleted))
            .Where(item => item.TenantId == tenantId
                && item.Module == "Legal"
                && item.EntityType == matter.EntityType
                && !item.IsDeleted)
            .FirstOrDefaultAsync(item => item.Fields.Any(field =>
                !field.IsDeleted
                && field.Key == "sourceProcedureCaseId"
                && field.Value == sourceProcedureCaseId.ToString())
                && item.Fields.Any(field =>
                    !field.IsDeleted
                    && field.Key == "matterPurpose"
                    && field.Value == matter.Purpose));
        if (existing is not null && !matter.AllowRepeat)
        {
            var existingMatterLinkedAt = DateTime.UtcNow;
            await AttachPropertyLegalHandoffDocumentsAsync(existing, sourceCase, matter, tenantId, userId, existingMatterLinkedAt);
            await LinkSourceCaseToLegalMatterAsync(sourceCase, existing, matter, tenantId, userId, existingMatterLinkedAt);
            return await ToDetailDtoAsync((await LoadCaseAsync(existing.Id, asTracking: false))!);
        }

        if (matter.RequiresGeneratedAgreement
            && string.IsNullOrWhiteSpace(FieldValue(sourceCase, "generatedAgreementReference")))
        {
            throw new InvalidOperationException("Generate the draft agreement before lodging it for Legal review.");
        }

        if (matter.RequiresExecutedAgreement
            && !string.Equals(FieldValue(sourceCase, "agreementExecutionStatus"), "Fully executed", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The agreement must be fully executed before conveyance and registration can begin.");
        }

        var sourceReference = FirstNonBlank(sourceCase.ReferenceNumber, FieldValue(sourceCase, "applicationReference"), sourceCase.Id.ToString())!;
        var propertyReference = FirstNonBlank(FieldValue(sourceCase, "propertyUnit"), FieldValue(sourceCase, "listingReference"));
        var applicant = FirstNonBlank(FieldValue(sourceCase, "customerName"), sourceCase.ApplicantName);
        var legalReference = $"LEG-{DateTime.UtcNow:yyyy}-{Guid.NewGuid():N}"[..17].ToUpperInvariant();
        var legalCase = await CreateCaseCoreAsync(new CreateProcedureCaseRequest(
            "Legal",
            matter.EntityType,
            $"{matter.Title}: {propertyReference ?? sourceReference}",
            legalReference,
            applicant,
            "Property Management",
            DateTime.UtcNow,
            request.Description ?? $"{matter.Title} lodged from Property Management transaction {sourceReference}.",
            new Dictionary<string, string?>
            {
                ["referenceNumber"] = legalReference,
                ["sourceProcedureCaseId"] = sourceCase.Id.ToString(),
                ["sourceEntityType"] = sourceCase.EntityType,
                ["sourceRecordReference"] = sourceReference,
                ["matterPurpose"] = matter.Purpose,
                ["transactionType"] = FieldValue(sourceCase, "requestType"),
                ["agreementReference"] = FieldValue(sourceCase, "generatedAgreementReference"),
                ["sourceDepartment"] = "Property Management",
                ["propertyFileReference"] = FirstNonBlank(FieldValue(sourceCase, "finalSignedAgreementReference"), FieldValue(sourceCase, "generatedAgreementReference"), sourceReference),
                ["propertyNumber"] = propertyReference,
                ["applicantName"] = applicant,
                ["receivedDate"] = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ["instrumentType"] = matter.InstrumentType,
                ["lesseeName"] = applicant,
                ["transfereeName"] = applicant,
                ["transferorName"] = "Rhema Systems & Associates Ltd",
                ["dueDiligenceStatus"] = "Not started",
                ["legalVettingStatus"] = "Under review"
            }), enforceLegalCreatePermission: false);

        var legalCaseEntity = await _db.ProcedureCases
            .Include(item => item.Documents.Where(document => !document.IsDeleted))
            .FirstAsync(item => item.TenantId == tenantId
                && item.Id == legalCase.Id
                && !item.IsDeleted);
        await AttachPropertyLegalHandoffDocumentsAsync(legalCaseEntity, sourceCase, matter, tenantId, userId, DateTime.UtcNow);

        var now = DateTime.UtcNow;
        await LinkSourceCaseToLegalMatterAsync(sourceCase, legalCase, matter, tenantId, userId, now);

        await RoleNotificationDispatcher.NotifyRolesAsync(
            _db,
            _notificationService,
            tenantId,
            userId,
            ["Legal Officer", "Legal Manager", "Head of Legal", "Legal Admin Assistant"],
            $"New property matter: {legalReference}",
            $"{matter.Title} for {propertyReference ?? sourceReference} was lodged by Property Management.",
            "legal.property-matter.created",
            "ProcedureCase",
            legalCase.Id,
            $"/legal/{Uri.EscapeDataString(matter.EntityType)}?caseId={legalCase.Id}",
            new Dictionary<string, object>
            {
                ["sourceModule"] = "PropertyManagement",
                ["sourceProcedureCaseId"] = sourceCase.Id,
                ["sourceReference"] = sourceReference,
                ["legalReference"] = legalReference,
                ["matterPurpose"] = matter.Purpose
            },
            CancellationToken.None);

        return legalCase;
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
        var wasLegalTransferPaymentReady = IsLegalTransferPaymentReady(procedureCase);
        var previousLegalTransferInterviewDate = FieldValue(procedureCase, "interviewDate");

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

        var fieldsByKey = procedureCase.Fields
            .Where(field => !string.IsNullOrWhiteSpace(field.Key))
            .GroupBy(field => field.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

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

        foreach (var (key, value) in request.FieldValues)
        {
            if (string.IsNullOrWhiteSpace(key) || fieldsByKey.ContainsKey(key))
            {
                continue;
            }

            var seed = ResolveProcedureFieldSeed(procedureCase, key);
            _db.ProcedureCaseFields.Add(new ProcedureCaseField
            {
                TenantId = tenantId,
                ProcedureCaseId = id,
                Key = key.Trim(),
                Label = seed?.Label ?? ToProcedureFieldLabel(key),
                FieldType = seed?.FieldType ?? "text",
                OptionsJson = seed?.Options is { Count: > 0 }
                    ? JsonSerializer.Serialize(seed.Options)
                    : null,
                Value = value,
                CreatedById = userId,
                CreatedAt = now,
                UpdatedAt = now,
                LastModifiedById = userId
            });
        }

        var groundRentAssetCode = await SyncEstateGroundRentAssessmentAsync(
            procedureCase,
            request.FieldValues,
            tenantId,
            userId,
            now);

        _db.ProcedureCaseActivities.Add(Activity(tenantId, userId, procedureCase.Id, "Updated intake", procedureCase.CurrentStageName, "Intake fields saved."));
        if (groundRentAssetCode != null)
        {
            _db.ProcedureCaseActivities.Add(Activity(
                tenantId,
                userId,
                procedureCase.Id,
                "Ground rent assessed",
                procedureCase.CurrentStageName,
                $"SOP ground-rent assessment synchronized to Estate asset {groundRentAssetCode}."));
        }
        await _db.SaveChangesAsync();

        var updatedCase = (await LoadCaseAsync(id, asTracking: false))!;
        await NotifyLegalTransferPaymentConfirmedAsync(
            updatedCase,
            wasLegalTransferPaymentReady,
            tenantId,
            userId,
            now);
        await NotifyLegalTransferInterviewDateChangedAsync(
            updatedCase,
            previousLegalTransferInterviewDate,
            tenantId,
            userId,
            now);

        return await ToDetailDtoAsync(updatedCase);
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

        return await ToDetailDtoAsync((await LoadCaseAsync(id, asTracking: false))!);
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
        var isContentUrlSave = IsProcedureCaseContentUrl(request.FileUrl);

        // File paths are assigned only by UploadDocumentAsync after managed storage accepts the file.
        // The metadata endpoint may retain the current content URL or clear it, but cannot bind another case's path.
        if (!isContentUrlSave && !string.IsNullOrWhiteSpace(request.FileUrl))
        {
            throw new InvalidOperationException("Upload procedure case documents through the secure procedure document upload endpoint.");
        }

        return await UpdateDocumentAttachmentAsync(
            procedureCase,
            document,
            request.FileName,
            isContentUrlSave ? document.FileUrl : null,
            request.Notes,
            tenantId,
            userId,
            isUpload: false);
    }

    private async Task<ProcedureCaseDetailDto> UpdateDocumentAttachmentAsync(
        ProcedureCase procedureCase,
        ProcedureCaseDocument document,
        string? fileName,
        string? fileUrl,
        string? notes,
        Guid tenantId,
        Guid userId,
        bool isUpload)
    {
        var now = DateTime.UtcNow;
        var hasFile = !string.IsNullOrWhiteSpace(fileUrl);
        var uploadedById = !hasFile ? null : isUpload ? userId : document.UploadedById;
        var uploadedAt = !hasFile ? null : isUpload ? now : document.UploadedAt;

        await _db.ProcedureCaseDocuments
            .IgnoreQueryFilters()
            .Where(item =>
                item.TenantId == tenantId
                && item.ProcedureCaseId == procedureCase.Id
                && item.Id == document.Id
                && !item.IsDeleted)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.FileName, hasFile
                    ? string.IsNullOrWhiteSpace(fileName) ? document.FileName : fileName
                    : null)
                .SetProperty(item => item.FileUrl, fileUrl)
                .SetProperty(item => item.Notes, notes)
                .SetProperty(item => item.UploadedById, uploadedById)
                .SetProperty(item => item.UploadedAt, uploadedAt)
                .SetProperty(item => item.UpdatedAt, now)
                .SetProperty(item => item.LastModifiedById, userId));

        await _db.ProcedureCases
            .IgnoreQueryFilters()
            .Where(item => item.TenantId == tenantId && item.Id == procedureCase.Id && !item.IsDeleted)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.LastActionById, userId)
                .SetProperty(item => item.UpdatedAt, now));

        _db.ProcedureCaseActivities.Add(Activity(tenantId, userId, procedureCase.Id, "Attached document", procedureCase.CurrentStageName, document.Name));
        await _db.SaveChangesAsync();

        return await ToDetailDtoAsync((await LoadCaseAsync(procedureCase.Id, asTracking: false))!);
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
            .Where(item => IsProcedureDocumentMandatoryForSubmission(procedureCase, item)
                && !IsProcedureDocumentSatisfied(procedureCase, item)
                && string.Equals(item.RequiredFrom, procedureCase.CurrentStageName, StringComparison.OrdinalIgnoreCase))
            .Select(item => item.Name)
            .ToList();

        if (missingDocuments.Count > 0)
        {
            throw new InvalidOperationException($"Upload required document(s) before submitting this stage: {string.Join(", ", missingDocuments)}.");
        }

        EnsureLegalTransferHeadMinutingReady(procedureCase);
        EnsureLegalTransferClientPaymentReady(procedureCase);
        EnsureLegalTransferRequiredStageFieldsReady(procedureCase);
        EnsureExternalListingApprovalIsReady(procedureCase);

        var tenantId = RequireTenantId();
        var userId = RequireUserId();
        var now = DateTime.UtcNow;

        await EnsureFacilitiesMaintenanceCloseoutReadyAsync(procedureCase, tenantId);

        var completedStageName = procedureCase.CurrentStageName;

        if (procedureCase.WorkflowInstanceId.HasValue)
        {
            // PR review: only write the stage-completed audit after the workflow engine successfully advances the instance.
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

            _db.ProcedureCaseActivities.Add(Activity(tenantId, userId, procedureCase.Id, "Completed stage", procedureCase.CurrentStageName, request.Notes));
            await _db.SaveChangesAsync();
            await SyncCaseFromWorkflowRuntimeAsync(procedureCase.Id, procedureCase.WorkflowInstanceId.Value, userId, request.Notes);
            var syncedCase = (await LoadCaseAsync(id, asTracking: false))!;
            await SynchronizeLinkedLegalMatterAsync(syncedCase, tenantId, userId, now);
            await ArchiveCompetingExternalListingRequestsAsync(syncedCase, tenantId, userId, now);
            await CreateMaintenanceJobCardForFacilitiesHandoffAsync(syncedCase, completedStageName, tenantId, userId, now);
            await NotifyLegalTransferPaymentRequestedAsync(syncedCase, completedStageName, tenantId, userId, now);
            await NotifyLegalTransferDraftReadyForClientAsync(syncedCase, completedStageName, tenantId, userId, now);
            await NotifyProcedureStageAssignedAsync(syncedCase, completedStageName, userId, tenantId);
            await NotifyEstateProcedureHandoffsAsync(syncedCase, completedStageName, syncedCase.CurrentStageName, userId, tenantId);
            return await ToDetailDtoAsync((await LoadCaseAsync(id, asTracking: false))!);
        }

        _db.ProcedureCaseActivities.Add(Activity(tenantId, userId, procedureCase.Id, "Completed stage", procedureCase.CurrentStageName, request.Notes));

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
        await SynchronizeLinkedLegalMatterAsync(updatedCase, tenantId, userId, now);
        await ArchiveCompetingExternalListingRequestsAsync(updatedCase, tenantId, userId, now);
        await CreateMaintenanceJobCardForFacilitiesHandoffAsync(updatedCase, completedStageName, tenantId, userId, now);
        await NotifyLegalTransferPaymentRequestedAsync(updatedCase, completedStageName, tenantId, userId, now);
        await NotifyLegalTransferDraftReadyForClientAsync(updatedCase, completedStageName, tenantId, userId, now);
        await NotifyProcedureStageAssignedAsync(updatedCase, completedStageName, userId, tenantId);
        await NotifyEstateProcedureHandoffsAsync(updatedCase, completedStageName, nextStage?.Name, userId, tenantId);

        return await ToDetailDtoAsync((await LoadCaseAsync(id, asTracking: false))!);
    }

    public async Task<ProcedureCaseDetailDto?> ApplyReviewActionAsync(
        Guid id,
        ReviewProcedureCaseRequest request)
    {
        var procedureCase = await LoadCaseAsync(id, asTracking: true);
        if (procedureCase is null)
        {
            return null;
        }

        EnsureCanEdit(procedureCase);
        if (!string.Equals(
                procedureCase.EntityType,
                "EstatePropertyManagementListingApplication",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Review actions are only available for property listing applications.");
        }

        var reason = request.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new InvalidOperationException("Enter a reason for this review action.");
        }

        var action = request.Action?.Trim();
        var isReject = string.Equals(action, "Reject", StringComparison.OrdinalIgnoreCase);
        var isClarification = string.Equals(action, "RequestClarification", StringComparison.OrdinalIgnoreCase);
        if (!isReject && !isClarification)
        {
            throw new InvalidOperationException("Select Reject or Return for clarification.");
        }
        if (isClarification && procedureCase.CurrentStageIndex == 0)
        {
            throw new InvalidOperationException("Stage 1 applications should be validated or rejected. Clarification return is available from Stage 2 onward.");
        }
        if (!procedureCase.WorkflowInstanceId.HasValue)
        {
            throw new InvalidOperationException("This application is not connected to an active workflow instance.");
        }

        var stepInstance = await _db.WorkflowStepInstances
            .Where(item => item.WorkflowInstanceId == procedureCase.WorkflowInstanceId.Value
                && item.WorkflowStepId == procedureCase.WorkflowStepId
                && (item.Status == WorkflowStepInstanceStatus.Pending
                    || item.Status == WorkflowStepInstanceStatus.InProgress))
            .OrderByDescending(item => item.CreatedDate)
            .FirstOrDefaultAsync();
        if (stepInstance is null)
        {
            throw new InvalidOperationException("The active workflow step could not be found.");
        }

        var tenantId = RequireTenantId();
        var userId = RequireUserId();
        var now = DateTime.UtcNow;
        var workflowResult = await _workflowEngine.ProcessStepAsync(
            stepInstance.Id,
            userId,
            isReject ? WorkflowStepAction.Reject : WorkflowStepAction.RequestInformation,
            new { procedureCase.Id, procedureCase.ReferenceNumber, Reason = reason },
            reason);
        if (!workflowResult.Success)
        {
            throw new InvalidOperationException(workflowResult.Message ?? "The workflow review action failed.");
        }

        if (isReject)
        {
            procedureCase.Status = "Rejected";
            procedureCase.CompletedAt = now;
            procedureCase.CurrentAssignedRole = null;
            procedureCase.CurrentStageOwner = null;
            UpsertLinkedSourceField(procedureCase, "applicationStatus", "Application status", "Rejected", userId, now);
            UpsertLinkedSourceField(procedureCase, "decisionStatus", "Management decision", "Rejected", userId, now);
            UpsertLinkedSourceField(procedureCase, "rejectionReason", "Rejection reason", reason, userId, now);
            UpsertLinkedSourceField(procedureCase, "customerNotificationStatus", "Customer notification status", "Rejection notified", userId, now);
        }
        else
        {
            procedureCase.Status = "Clarification required";
            UpsertLinkedSourceField(procedureCase, "applicationStatus", "Application status", "Clarification required", userId, now);
            UpsertLinkedSourceField(procedureCase, "clarificationReason", "Clarification requested", reason, userId, now);
            UpsertLinkedSourceField(procedureCase, "customerNotificationStatus", "Customer notification status", "Clarification requested", userId, now);
        }

        procedureCase.LastActionById = userId;
        procedureCase.UpdatedAt = now;
        procedureCase.Activities.Add(Activity(
            tenantId,
            userId,
            procedureCase.Id,
            isReject ? "Rejected application" : "Requested clarification",
            procedureCase.CurrentStageName,
            reason));
        await _db.SaveChangesAsync();

        if (procedureCase.OpenedById != Guid.Empty)
        {
            try
            {
                await _notificationService.CreateNotificationAsync(
                    new CreateNotificationDto
                    {
                        RecipientId = procedureCase.OpenedById,
                        Type = isReject
                            ? "estate.property.application-rejected"
                            : "estate.property.application-clarification",
                        Title = isReject
                            ? "Property application rejected"
                            : "Property application needs clarification",
                        Message = $"{procedureCase.ReferenceNumber ?? procedureCase.Title}: {reason}",
                        Priority = isReject ? "High" : "Normal",
                        EntityType = "ProcedureCase",
                        EntityId = procedureCase.Id,
                        ActionUrl = $"/external-portal/my-property-requests/{procedureCase.Id}"
                    },
                    userId,
                    tenantId);
            }
            catch
            {
                // Notification delivery must not roll back the governed review action.
            }
        }

        return await ToDetailDtoAsync((await LoadCaseAsync(id, asTracking: false))!);
    }

    public async Task SyncFromWorkflowRuntimeAsync(
        Guid procedureCaseId,
        Guid workflowInstanceId,
        Guid actorUserId,
        string? notes = null)
    {
        await SyncCaseFromWorkflowRuntimeAsync(procedureCaseId, workflowInstanceId, actorUserId, notes);
        var procedureCase = await LoadCaseAsync(procedureCaseId, asTracking: false);
        if (procedureCase is not null)
        {
            await SynchronizeLinkedLegalMatterAsync(
                procedureCase,
                procedureCase.TenantId,
                actorUserId,
                DateTime.UtcNow);
        }
    }

    private async Task SynchronizeLinkedLegalMatterAsync(
        ProcedureCase legalCase,
        Guid tenantId,
        Guid actorUserId,
        DateTime now)
    {
        if (!string.Equals(legalCase.Module, "Legal", StringComparison.OrdinalIgnoreCase)
            || !Guid.TryParse(FieldValue(legalCase, "sourceProcedureCaseId"), out var sourceCaseId))
        {
            return;
        }

        var sourceCase = await _db.ProcedureCases
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId
                && item.Id == sourceCaseId
                && !item.IsDeleted)
            .Select(item => new
            {
                item.Id,
                item.OpenedById,
                item.ReferenceNumber,
                item.Title,
                item.CurrentStageName
            })
            .FirstOrDefaultAsync();
        if (sourceCase is null)
        {
            return;
        }

        var sourceFields = await _db.ProcedureCaseFields
            .AsNoTracking()
            .Where(field => field.TenantId == tenantId
                && field.ProcedureCaseId == sourceCase.Id
                && !field.IsDeleted)
            .Select(field => new { field.Key, field.Value })
            .ToListAsync();
        string? SourceFieldValue(string key) => sourceFields
            .FirstOrDefault(field => string.Equals(field.Key, key, StringComparison.OrdinalIgnoreCase))
            ?.Value;

        var purpose = FieldValue(legalCase, "matterPurpose") ?? legalCase.EntityType;
        var isCompleted = IsCompleted(legalCase);
        var status = isCompleted ? "Completed" : $"Open - {legalCase.CurrentStageName}";
        string? customerLegalReviewStatus = null;
        var previousCustomerLegalReviewStatus = SourceFieldValue("legalAgreementReviewStatus");
        await UpsertLinkedSourceFieldAsync(tenantId, sourceCase.Id, "legalLastMatterCaseId", "Latest linked Legal matter ID", legalCase.Id.ToString(), actorUserId, now);
        await UpsertLinkedSourceFieldAsync(tenantId, sourceCase.Id, "legalLastMatterReference", "Latest linked Legal matter reference", legalCase.ReferenceNumber, actorUserId, now);
        await UpsertLinkedSourceFieldAsync(tenantId, sourceCase.Id, "legalLastMatterType", "Latest linked Legal matter type", purpose, actorUserId, now);
        await UpsertLinkedSourceFieldAsync(tenantId, sourceCase.Id, "legalLastMatterStatus", "Latest linked Legal matter status", status, actorUserId, now);

        if (string.Equals(purpose, "AgreementReview", StringComparison.OrdinalIgnoreCase))
        {
            var vettingStatus = FieldValue(legalCase, "legalVettingStatus");
            var returned = string.Equals(vettingStatus, "Returned for correction", StringComparison.OrdinalIgnoreCase)
                || string.Equals(vettingStatus, "Returned", StringComparison.OrdinalIgnoreCase);
            var approved = isCompleted
                && !returned;
            customerLegalReviewStatus = approved
                ? "Approved by Legal"
                : returned
                    ? "Returned by Legal for correction"
                    : $"Under Legal review - {legalCase.CurrentStageName}";
            await UpsertLinkedSourceFieldAsync(
                tenantId,
                sourceCase.Id,
                "legalAgreementReviewStatus",
                "Legal agreement review status",
                customerLegalReviewStatus,
                actorUserId,
                now);
        }
        else if (string.Equals(purpose, "ConveyanceRegistration", StringComparison.OrdinalIgnoreCase))
        {
            await UpsertLinkedSourceFieldAsync(
                tenantId,
                sourceCase.Id,
                "legalConveyanceStatus",
                "Legal conveyance / registration status",
                isCompleted ? "Completed by Legal" : $"Open - {legalCase.CurrentStageName}",
                actorUserId,
                now);
            await UpsertLinkedSourceFieldAsync(
                tenantId,
                sourceCase.Id,
                "ownershipTransferStatus",
                "Ownership transfer status",
                isCompleted ? "Ready for Estate records amendment" : "Blocked - Legal conveyance and registration pending",
                actorUserId,
                now);
        }

        await _db.ProcedureCases
            .IgnoreQueryFilters()
            .Where(item => item.TenantId == tenantId
                && item.Id == sourceCase.Id
                && !item.IsDeleted)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.LastActionById, actorUserId)
                .SetProperty(item => item.UpdatedAt, now));

        _db.ProcedureCaseActivities.Add(Activity(
            tenantId,
            actorUserId,
            sourceCase.Id,
            "Legal matter status updated",
            sourceCase.CurrentStageName,
            $"{legalCase.ReferenceNumber}: {status}."));
        await _db.SaveChangesAsync();

        var customerReviewChanged = !string.Equals(
            previousCustomerLegalReviewStatus,
            customerLegalReviewStatus,
            StringComparison.OrdinalIgnoreCase);
        var customerShouldBeNotified = customerReviewChanged
            && sourceCase.OpenedById != Guid.Empty
            && (string.Equals(customerLegalReviewStatus, "Approved by Legal", StringComparison.OrdinalIgnoreCase)
                || string.Equals(customerLegalReviewStatus, "Returned by Legal for correction", StringComparison.OrdinalIgnoreCase));
        if (customerShouldBeNotified)
        {
            var approved = string.Equals(customerLegalReviewStatus, "Approved by Legal", StringComparison.OrdinalIgnoreCase);
            try
            {
                await _notificationService.CreateNotificationAsync(
                    new CreateNotificationDto
                    {
                        RecipientId = sourceCase.OpenedById,
                        Type = approved ? "estate.property.agreement-released" : "estate.property.agreement-correction-required",
                        Title = approved ? "Property agreement ready for review" : "Property agreement requires correction",
                        Message = approved
                            ? $"Legal has approved the agreement for {sourceCase.ReferenceNumber ?? sourceCase.Title}. You can now review and accept it."
                            : $"Legal returned the agreement for {sourceCase.ReferenceNumber ?? sourceCase.Title} for correction.",
                        Priority = approved ? "High" : "Normal",
                        EntityType = "ProcedureCase",
                        EntityId = sourceCase.Id,
                        ActionUrl = $"/external-portal/my-property-requests/{sourceCase.Id}",
                        Metadata = new Dictionary<string, object>
                        {
                            ["legalCaseId"] = legalCase.Id,
                            ["legalReference"] = legalCase.ReferenceNumber ?? string.Empty,
                            ["status"] = customerLegalReviewStatus ?? string.Empty
                        }
                    },
                    actorUserId,
                    tenantId);
            }
            catch
            {
                // Notification delivery must not block completion of the Legal workflow.
            }
        }

        await RoleNotificationDispatcher.NotifyRolesAsync(
            _db,
            _notificationService,
            tenantId,
            actorUserId,
            ["Property Manager", "Property Officer", "Estate Manager", "Head of Estate"],
            $"Legal matter {legalCase.ReferenceNumber}: {status}",
            $"Legal updated {purpose} for {sourceCase.ReferenceNumber ?? sourceCase.Title}.",
            "legal.property-matter.status",
            "ProcedureCase",
            sourceCase.Id,
            $"/estate/property-management/EstatePropertyManagementListingApplication?caseId={sourceCase.Id}",
            new Dictionary<string, object>
            {
                ["legalCaseId"] = legalCase.Id,
                ["legalReference"] = legalCase.ReferenceNumber ?? string.Empty,
                ["matterPurpose"] = purpose,
                ["status"] = status
            },
            CancellationToken.None);
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

        return await UpdateDocumentAttachmentAsync(
            procedureCase,
            document,
            upload.OriginalFileName,
            upload.FilePath,
            notes,
            RequireTenantId(),
            RequireUserId(),
            isUpload: true);
    }

    public async Task<ProcedureCaseDetailDto?> UploadCustomerIntakeDocumentAsync(
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

        var userId = RequireUserId();
        if (procedureCase.OpenedById != userId
            || !string.Equals(procedureCase.SourceDepartment, "External Portal - Estate Listings", StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Only the customer who submitted this property request can upload its intake documents.");
        }

        if (IsCompleted(procedureCase))
        {
            throw new InvalidOperationException("Completed property requests cannot receive new intake documents.");
        }

        if (HasFirstInternalStageBeenRoutedForward(procedureCase))
        {
            throw new InvalidOperationException("Customer intake documents can only be uploaded until the first internal stage is routed forward.");
        }

        var document = procedureCase.Documents.FirstOrDefault(item => item.Id == documentId);
        if (document is null)
        {
            return null;
        }

        if (!string.Equals(document.ProvidedBy, "Customer", StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Customers cannot upload documents into this internal document category.");
        }

        var upload = await _fileStorageService.UploadFileAsync(new FileUploadRequest
        {
            FileStream = fileStream,
            FileName = fileName,
            ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
            FileSize = fileSize,
            Category = "procedure-case-customer-intake-documents",
            TenantId = RequireTenantId().ToString(),
            OverwriteExisting = false
        });

        if (!upload.Success)
        {
            throw new InvalidOperationException(upload.ErrorMessage ?? "Customer intake document upload failed.");
        }

        return await UpdateDocumentAttachmentAsync(
            procedureCase,
            document,
            upload.OriginalFileName,
            upload.FilePath,
            notes,
            RequireTenantId(),
            userId,
            isUpload: true);
    }

    private static string ResolveDocumentProvider(string documentName, string? configuredProvider)
    {
        if (!string.IsNullOrWhiteSpace(configuredProvider)
            && !string.Equals(configuredProvider, "Internal", StringComparison.OrdinalIgnoreCase))
        {
            return configuredProvider.Trim();
        }

        return documentName.StartsWith("Customer identity or eligibility evidence", StringComparison.OrdinalIgnoreCase)
            || documentName.StartsWith("Offer, financing, or purchase supporting evidence", StringComparison.OrdinalIgnoreCase)
            || documentName.StartsWith("Rental application or tenancy supporting evidence", StringComparison.OrdinalIgnoreCase)
                ? "Customer"
                : "Internal";
    }

    private static string NormalizeDocumentApplicability(string? appliesTo)
    {
        if (string.Equals(appliesTo, "Rent", StringComparison.OrdinalIgnoreCase)
            || string.Equals(appliesTo, "Rental", StringComparison.OrdinalIgnoreCase))
        {
            return "Rent";
        }

        if (string.Equals(appliesTo, "Sale", StringComparison.OrdinalIgnoreCase)
            || string.Equals(appliesTo, "Purchase", StringComparison.OrdinalIgnoreCase))
        {
            return "Sale";
        }

        return "All";
    }

    private static string? ResolveRequestApplicability(IDictionary<string, string?>? fieldValues)
    {
        if (fieldValues is null)
        {
            return null;
        }

        var requestType = fieldValues.FirstOrDefault(item =>
            string.Equals(item.Key, "requestType", StringComparison.OrdinalIgnoreCase)).Value;
        var listingType = fieldValues.FirstOrDefault(item =>
            string.Equals(item.Key, "listingType", StringComparison.OrdinalIgnoreCase)).Value;
        var transactionType = $"{requestType} {listingType}";

        if (transactionType.Contains("purchase", StringComparison.OrdinalIgnoreCase)
            || transactionType.Contains("sale", StringComparison.OrdinalIgnoreCase)
            || transactionType.Contains("buy", StringComparison.OrdinalIgnoreCase))
        {
            return "Sale";
        }

        if (transactionType.Contains("rent", StringComparison.OrdinalIgnoreCase)
            || transactionType.Contains("rental", StringComparison.OrdinalIgnoreCase)
            || transactionType.Contains("lease", StringComparison.OrdinalIgnoreCase))
        {
            return "Rent";
        }

        return null;
    }

    private static bool DocumentAppliesToRequest(string? appliesTo, string? requestApplicability)
    {
        var normalized = NormalizeDocumentApplicability(appliesTo);
        return normalized == "All"
            || string.IsNullOrWhiteSpace(requestApplicability)
            || string.Equals(normalized, requestApplicability, StringComparison.OrdinalIgnoreCase);
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

    public async Task<ProcedureCaseDetailDto?> SignLegalTransferExecutedDocumentAsync(
        Guid id,
        Guid documentId,
        SignProcedureCaseDocumentRequest request)
    {
        var procedureCase = await LoadCaseAsync(id, asTracking: true);
        if (procedureCase is null)
        {
            return null;
        }

        EnsureCanEdit(procedureCase);
        if (!string.Equals(procedureCase.EntityType, "LegalTransfer", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only Legal transfer cases can use this signing action.");
        }

        var signatureRole = LegalTransferSignatureRoleForStage(procedureCase.CurrentStageName);
        if (signatureRole is null)
        {
            throw new InvalidOperationException("This legal transfer stage does not allow signing the executed transfer form.");
        }

        if (!string.IsNullOrWhiteSpace(request.SignatureRole)
            && !string.Equals(request.SignatureRole.Trim(), signatureRole, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"This stage must be signed as {signatureRole}.");
        }

        var document = procedureCase.Documents.FirstOrDefault(item => item.Id == documentId && !item.IsDeleted);
        if (document is null)
        {
            return null;
        }

        if (!string.Equals(document.Name, "Executed transfer form", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Legal signatures must be applied to the executed transfer form submitted by the client.");
        }

        if (string.IsNullOrWhiteSpace(document.FileUrl))
        {
            throw new InvalidOperationException("The executed transfer form has not been submitted yet.");
        }

        if (Uri.TryCreate(document.FileUrl, UriKind.Absolute, out _)
            || document.FileUrl.StartsWith("/document-management", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("This signing action only supports the uploaded executed transfer form.");
        }

        if (IsLegalTransferStageSignatureRecorded(procedureCase, document))
        {
            throw new InvalidOperationException($"{signatureRole} has already signed the executed transfer form.");
        }

        var tenantId = RequireTenantId();
        var userId = RequireUserId();
        var actor = string.IsNullOrWhiteSpace(_currentUser.FullName)
            ? _currentUser.UserName ?? "System"
            : _currentUser.FullName.Trim();
        var now = DateTime.UtcNow;

        await using var sourceStream = await _fileStorageService.DownloadFileAsync(document.FileUrl, document.Id);
        var signature = await _pdfSigningService.SignAsync(
            sourceStream,
            new CentralDocumentPdfSigningRequest(
                procedureCase.ReferenceNumber ?? procedureCase.Title,
                actor,
                signatureRole,
                request.Notes,
                now),
            CancellationToken.None);

        await using var signedStream = new MemoryStream(signature.PdfBytes);
        var signedFileName = BuildSignedProcedureDocumentFileName(document.FileName ?? document.Name, signatureRole);
        var upload = await _fileStorageService.UploadFileAsync(new FileUploadRequest
        {
            FileStream = signedStream,
            FileName = signedFileName,
            ContentType = "application/pdf",
            FileSize = signature.PdfBytes.LongLength,
            Category = "procedure-case-documents",
            TenantId = tenantId.ToString(),
            Metadata = new Dictionary<string, string>
            {
                ["SourceDocumentId"] = document.Id.ToString(),
                ["SignatureRole"] = signatureRole,
                ["SignedBy"] = actor,
                ["SignedAtUtc"] = now.ToString("O"),
                ["SignatureField"] = signature.SignatureFieldName,
                ["SignatureCertificateThumbprint"] = signature.CertificateThumbprint,
                ["SignedDocumentSha256"] = signature.DocumentSha256
            },
            OverwriteExisting = false
        });

        if (!upload.Success)
        {
            throw new InvalidOperationException(upload.ErrorMessage ?? "Signed transfer document upload failed.");
        }

        document.FileName = upload.OriginalFileName;
        document.FileUrl = upload.FilePath;
        document.Notes = string.IsNullOrWhiteSpace(request.Notes)
            ? $"{signatureRole} digitally signed by {actor}."
            : request.Notes.Trim();
        document.UploadedById = userId;
        document.UploadedAt = now;
        document.UpdatedAt = now;
        document.LastModifiedById = userId;

        var fields = procedureCase.Fields
            .Where(field => !field.IsDeleted)
            .ToDictionary(field => field.Key, field => field, StringComparer.OrdinalIgnoreCase);
        if (string.Equals(procedureCase.CurrentStageName, "Legal Officer Signature", StringComparison.OrdinalIgnoreCase))
        {
            UpsertProcedureCaseField(procedureCase, fields, "signatureStatus", "Signature status", "select", "Legal signed", userId, now);
        }
        else if (string.Equals(procedureCase.CurrentStageName, "Head of Legal Signature", StringComparison.OrdinalIgnoreCase))
        {
            UpsertProcedureCaseField(procedureCase, fields, "signatureStatus", "Signature status", "select", "Head of Legal signed", userId, now);
        }

        procedureCase.LastActionById = userId;
        procedureCase.LastModifiedById = userId;
        procedureCase.UpdatedAt = now;

        _db.ProcedureCaseActivities.Add(Activity(
            tenantId,
            userId,
            procedureCase.Id,
            "Signed executed transfer form",
            procedureCase.CurrentStageName,
            $"{signatureRole} signature applied to {upload.OriginalFileName}."));
        await _db.SaveChangesAsync();

        return await ToDetailDtoAsync((await LoadCaseAsync(id, asTracking: false))!);
    }

    public async Task<ProcedureCaseDetailDto?> SyncLegalTransferFeePaymentStatusAsync(Guid id)
    {
        var procedureCase = await LoadCaseAsync(id, asTracking: false);
        if (procedureCase is null)
        {
            return null;
        }

        EnsureCanEdit(procedureCase);
        if (!IsLegalTransferClientPaymentStage(procedureCase))
        {
            throw new InvalidOperationException("Transfer fee payment can only be synced during Client Payment Call.");
        }

        var tenantId = RequireTenantId();
        var userId = RequireUserId();
        var now = DateTime.UtcNow;

        var invoice = await FindLegalTransferFeeInvoiceAsync(procedureCase, tenantId);
        if (invoice is null)
        {
            var paymentRequestReference = BuildLegalTransferFeePaymentRequestReference(procedureCase);
            throw new InvalidOperationException(
                $"Finance has not created a transfer-fee AR invoice with reference '{paymentRequestReference}' yet.");
        }

        var receiptNumber = await _db.Set<PaymentAllocation>()
            .AsNoTracking()
            .Where(allocation => allocation.TenantId == tenantId
                && !allocation.IsDeleted
                && !allocation.IsReversal
                && allocation.InvoiceId == invoice.Id
                && !allocation.CustomerPayment.IsDeleted
                && allocation.CustomerPayment.Status != "Cancelled"
                && allocation.CustomerPayment.Status != "Bounced")
            .OrderByDescending(allocation => allocation.AllocationDate)
            .Select(allocation => allocation.CustomerPayment.PaymentNumber)
            .FirstOrDefaultAsync();

        var wasPaymentReady = IsLegalTransferPaymentReady(procedureCase);
        var transferFeePayable = ParseProcedureAmount(FieldValue(procedureCase, "transferFeePayable"));
        var invoiceAmountMatches = !transferFeePayable.HasValue || AmountsMatch(invoice.TotalAmount, transferFeePayable.Value);
        var invoiceIsPaid = invoice.BalanceAmount <= 0m && invoice.Status == InvoiceStatus.Paid;
        var paymentStatus = invoiceAmountMatches && invoiceIsPaid
            ? "Paid"
            : "Pending";
        var paymentCheckStatus = invoiceAmountMatches
            ? invoiceIsPaid
                ? "Paid in full"
                : $"Awaiting full payment; balance {invoice.CurrencyCode} {invoice.BalanceAmount:N2}"
            : $"Invoice total {invoice.CurrencyCode} {invoice.TotalAmount:N2} does not match transfer fee {invoice.CurrencyCode} {transferFeePayable!.Value:N2}";

        await UpsertLinkedSourceFieldAsync(tenantId, id, "transferFeeInvoiceId", "Transfer fee invoice ID", invoice.Id.ToString(), userId, now);
        await UpsertLinkedSourceFieldAsync(tenantId, id, "transferFeeInvoiceReference", "Transfer fee invoice reference", invoice.InvoiceNumber, userId, now);
        await UpsertLinkedSourceFieldAsync(tenantId, id, "transferFeeInvoiceStatus", "Transfer fee invoice status", invoice.Status.ToString(), userId, now);
        await UpsertLinkedSourceFieldAsync(tenantId, id, "transferFeeInvoiceAmount", "Transfer fee invoice amount", invoice.TotalAmount.ToString("0.00", CultureInfo.InvariantCulture), userId, now);
        await UpsertLinkedSourceFieldAsync(tenantId, id, "transferFeeInvoicePaidAmount", "Transfer fee invoice paid amount", invoice.PaidAmount.ToString("0.00", CultureInfo.InvariantCulture), userId, now);
        await UpsertLinkedSourceFieldAsync(tenantId, id, "transferFeeInvoiceBalance", "Transfer fee invoice balance", invoice.BalanceAmount.ToString("0.00", CultureInfo.InvariantCulture), userId, now);
        await UpsertLinkedSourceFieldAsync(tenantId, id, "transferFeePaymentCheckStatus", "Transfer fee payment check", paymentCheckStatus, userId, now);
        await UpsertLinkedSourceFieldAsync(tenantId, id, "paymentStatus", "Payment status", paymentStatus, userId, now);
        if (IsCustomerVisibleInvoiceStatus(invoice.Status) && invoice.BalanceAmount > 0m)
        {
            await NotifyLegalTransferFeeInvoiceCustomerReadyAsync(procedureCase, invoice, tenantId, userId, now);
        }

        if (!string.IsNullOrWhiteSpace(receiptNumber))
        {
            await UpsertLinkedSourceFieldAsync(tenantId, id, "paymentReceiptReference", "Payment / receipt reference", receiptNumber, userId, now);
            await UpsertLinkedSourceFieldAsync(tenantId, id, "transferFeeReceipt", "Transfer fee receipt", receiptNumber, userId, now);
        }

        _db.ProcedureCaseActivities.Add(Activity(
            tenantId,
            userId,
            id,
            "Transfer fee payment synced",
            procedureCase.CurrentStageName,
            string.IsNullOrWhiteSpace(receiptNumber)
                ? $"Finance invoice {invoice.InvoiceNumber} is {invoice.Status}; {paymentCheckStatus}."
                : $"Finance receipt {receiptNumber} applied to invoice {invoice.InvoiceNumber}."));

        await _db.SaveChangesAsync();

        var updatedCase = (await LoadCaseAsync(id, asTracking: false))!;
        await NotifyLegalTransferPaymentConfirmedAsync(updatedCase, wasPaymentReady, tenantId, userId, now);
        return await ToDetailDtoAsync(updatedCase);
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

    private async Task CreateMaintenanceJobCardForFacilitiesHandoffAsync(
        ProcedureCase procedureCase,
        string completedStageName,
        Guid tenantId,
        Guid userId,
        DateTime now)
    {
        if (!string.Equals(procedureCase.Module, "Facilities", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(procedureCase.EntityType, "EstateFacilityMaintenance", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(completedStageName, "Maintenance Handoff Review", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var existingJobCardId = FieldValue(procedureCase, "maintenanceJobCardId");
        var existingJobCardReference = FieldValue(procedureCase, "maintenanceJobCardReference");
        if (Guid.TryParse(existingJobCardId, out var linkedJobCardId))
        {
            var linkedJobCard = await _jobCardService.GetJobCardByIdAsync(linkedJobCardId);
            if (linkedJobCard is not null)
            {
                await SyncFacilitiesMaintenanceJobCardFieldsAsync(procedureCase.Id, linkedJobCard, tenantId, userId, now);
                return;
            }
        }

        if (!string.IsNullOrWhiteSpace(existingJobCardReference))
        {
            var linkedJobCard = await _jobCardService.GetJobCardByNumberAsync(existingJobCardReference);
            if (linkedJobCard is not null)
            {
                await SyncFacilitiesMaintenanceJobCardFieldsAsync(procedureCase.Id, linkedJobCard, tenantId, userId, now);
                return;
            }
        }

        var sourceCaseMarker = procedureCase.Id.ToString();
        var existingFromMarker = await _db.JobCards
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId
                && !item.IsDeleted
                && item.CustomFieldValues != null
                && item.CustomFieldValues.Contains(sourceCaseMarker))
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefaultAsync();
        if (existingFromMarker is not null)
        {
            var linkedJobCard = await _jobCardService.GetJobCardByIdAsync(existingFromMarker.Id);
            if (linkedJobCard is not null)
            {
                await SyncFacilitiesMaintenanceJobCardFieldsAsync(procedureCase.Id, linkedJobCard, tenantId, userId, now);
                return;
            }
        }

        var maintenanceAsset = await ResolveFacilitiesMaintenanceAssetAsync(procedureCase, tenantId);
        var maintenanceType = await ResolveFacilitiesMaintenanceTypeAsync(procedureCase, tenantId);
        var priorityLevel = await ResolveFacilitiesPriorityLevelAsync(procedureCase, tenantId);
        if (maintenanceAsset is null || maintenanceType is null || priorityLevel is null)
        {
            throw new InvalidOperationException("Maintenance setup is incomplete. Configure an active maintenance asset, maintenance type, and priority level before routing this Facilities request.");
        }

        var sourceReference = FirstNonBlank(procedureCase.ReferenceNumber, procedureCase.Title, procedureCase.Id.ToString()) ?? procedureCase.Id.ToString();
        var propertyUnit = FirstNonBlank(FieldValue(procedureCase, "propertyUnit"), FieldValue(procedureCase, "propertyNumber"), FieldValue(procedureCase, "housePlotShopNumber"));
        var issueType = FirstNonBlank(FieldValue(procedureCase, "issueType"), "Maintenance request")!;
        var issueDescription = FirstNonBlank(FieldValue(procedureCase, "issueDescription"), procedureCase.Description, FieldValue(procedureCase, "notes"));
        var serviceImpact = FirstNonBlank(FieldValue(procedureCase, "serviceImpact"), "Not recorded")!;
        var accessInstructions = FirstNonBlank(FieldValue(procedureCase, "accessInstructions"), "Not recorded")!;
        var targetDate = ParseProcedureDate(FieldValue(procedureCase, "targetDate"));
        var customerBusinessPartnerId = Guid.TryParse(FieldValue(procedureCase, "sourceReference"), out var parsedCustomerId)
            ? parsedCustomerId
            : (Guid?)null;

        var createdJobCard = await _jobCardService.CreateJobCardAsync(new CreateJobCardDto
        {
            Title = $"Facilities maintenance - {FirstNonBlank(propertyUnit, issueType, sourceReference)}",
            Description = $"Source: Estate / Facilities. Facilities case {sourceReference}. Property/unit: {propertyUnit ?? "Not recorded"}. Service impact: {serviceImpact}. Access: {accessInstructions}.",
            ProblemDescription = issueDescription ?? issueType,
            AssetId = maintenanceAsset.Id,
            MaintenanceTypeId = maintenanceType.Id,
            PriorityLevelId = priorityLevel.Id,
            CustomerBusinessPartnerId = customerBusinessPartnerId,
            MaintenanceLocation = "External",
            RequiredCompletionDate = targetDate,
            EstimatedHours = maintenanceType.EstimatedHours > 0 ? maintenanceType.EstimatedHours : 2,
            EstimatedCost = maintenanceType.EstimatedCost,
            RequiresShutdown = maintenanceType.RequiresShutdown,
            RequiresSafetyPermit = maintenanceType.RequiresSafetyPermit,
            SafetyRequirements = FirstNonBlank(maintenanceType.SafetyRequirements, FieldValue(procedureCase, "safetyNotes")),
            SpecialInstructions = $"Facilities routing approved from {completedStageName}. Continue execution in Maintenance Management and return job card/work order status to Facilities closeout.",
            CustomFieldValues = new Dictionary<string, object>
            {
                ["sourceModule"] = "Estate / Facilities",
                ["sourceProcedureCaseId"] = procedureCase.Id.ToString(),
                ["sourceReference"] = sourceReference,
                ["sourceEntityType"] = procedureCase.EntityType,
                ["propertyUnit"] = propertyUnit ?? string.Empty,
                ["issueType"] = issueType,
                ["serviceImpact"] = serviceImpact
            }
        });

        await SyncFacilitiesMaintenanceJobCardFieldsAsync(procedureCase.Id, createdJobCard, tenantId, userId, now);
        await RoleNotificationDispatcher.NotifyRolesAsync(
            _db,
            _notificationService,
            tenantId,
            userId,
            ["Maintenance Manager", "Maintenance Officer", "Facilities Manager"],
            $"Maintenance job card created: {createdJobCard.JobCardNumber}",
            $"{createdJobCard.JobCardNumber} was created from Facilities case {sourceReference} for {propertyUnit ?? "the reported property/unit"}.",
            "facilities.maintenance-job-card.created",
            "JobCard",
            createdJobCard.Id,
            "/maintenance/job-cards",
            new Dictionary<string, object>
            {
                ["sourceModule"] = "Estate / Facilities",
                ["sourceProcedureCaseId"] = procedureCase.Id,
                ["sourceRecordReference"] = sourceReference,
                ["jobCardId"] = createdJobCard.Id,
                ["jobCardNumber"] = createdJobCard.JobCardNumber,
                ["propertyUnit"] = propertyUnit ?? string.Empty,
                ["issueType"] = issueType
            },
            CancellationToken.None);

        _db.ProcedureCaseActivities.Add(Activity(
            tenantId,
            userId,
            procedureCase.Id,
            "Maintenance job card created",
            procedureCase.CurrentStageName,
            $"{createdJobCard.JobCardNumber} opened in Maintenance Management."));
        await _db.SaveChangesAsync();
    }

    private async Task SyncFacilitiesMaintenanceJobCardFieldsAsync(
        Guid procedureCaseId,
        JobCardDto jobCard,
        Guid tenantId,
        Guid userId,
        DateTime now)
    {
        await UpsertLinkedSourceFieldAsync(tenantId, procedureCaseId, "maintenanceJobCardId", "Maintenance job card ID", jobCard.Id.ToString(), userId, now);
        await UpsertLinkedSourceFieldAsync(tenantId, procedureCaseId, "maintenanceJobCardReference", "Maintenance job card reference", jobCard.JobCardNumber, userId, now);
        await UpsertLinkedSourceFieldAsync(tenantId, procedureCaseId, "maintenanceHandoffStatus", "Maintenance handoff status", $"Job card created - {jobCard.JobCardStatus}", userId, now);
        await UpsertLinkedSourceFieldAsync(tenantId, procedureCaseId, "maintenanceHandoffAt", "Maintenance handoff at", now.ToString("O", CultureInfo.InvariantCulture), userId, now);
        await _db.SaveChangesAsync();
    }

    private async Task EnsureFacilitiesMaintenanceCloseoutReadyAsync(
        ProcedureCase procedureCase,
        Guid tenantId)
    {
        if (!string.Equals(procedureCase.Module, "Facilities", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(procedureCase.EntityType, "EstateFacilityMaintenance", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(procedureCase.CurrentStageName, "Maintenance Closeout", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var jobCard = await ResolveFacilitiesMaintenanceJobCardAsync(procedureCase);
        if (jobCard is null)
        {
            throw new InvalidOperationException("Maintenance closeout cannot be submitted until the linked Maintenance job card is available.");
        }

        if (jobCard.GeneratedWorkOrderId.HasValue)
        {
            var workOrder = await _db.WorkOrders
                .AsNoTracking()
                .Where(item => item.TenantId == tenantId
                    && !item.IsDeleted
                    && item.Id == jobCard.GeneratedWorkOrderId.Value)
                .Select(item => new { item.WorkOrderNumber, item.Status })
                .FirstOrDefaultAsync();

            if (workOrder is not null && !IsMaintenanceExecutionComplete(workOrder.Status))
            {
                throw new InvalidOperationException($"Maintenance closeout cannot be submitted because work order {workOrder.WorkOrderNumber} is still {workOrder.Status}.");
            }
        }

        if (!IsMaintenanceExecutionComplete(jobCard.JobCardStatus))
        {
            throw new InvalidOperationException($"Maintenance closeout cannot be submitted because job card {jobCard.JobCardNumber} is still {jobCard.JobCardStatus}.");
        }
    }

    private async Task<JobCardDto?> ResolveFacilitiesMaintenanceJobCardAsync(ProcedureCase procedureCase)
    {
        var jobCardId = FieldValue(procedureCase, "maintenanceJobCardId");
        if (Guid.TryParse(jobCardId, out var linkedJobCardId))
        {
            var byId = await _jobCardService.GetJobCardByIdAsync(linkedJobCardId);
            if (byId is not null)
            {
                return byId;
            }
        }

        var jobCardReference = FieldValue(procedureCase, "maintenanceJobCardReference");
        return string.IsNullOrWhiteSpace(jobCardReference)
            ? null
            : await _jobCardService.GetJobCardByNumberAsync(jobCardReference);
    }

    private static bool IsMaintenanceExecutionComplete(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return false;
        }

        return status.Trim().Equals("Completed", StringComparison.OrdinalIgnoreCase)
            || status.Trim().Equals("Quality Checked", StringComparison.OrdinalIgnoreCase)
            || status.Trim().Equals("Accepted", StringComparison.OrdinalIgnoreCase)
            || status.Trim().Equals("Closed", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<ErpSystem.Core.Entities.Maintenance.MaintenanceAsset?> ResolveFacilitiesMaintenanceAssetAsync(
        ProcedureCase procedureCase,
        Guid tenantId)
    {
        var propertyUnit = FirstNonBlank(FieldValue(procedureCase, "propertyUnit"), FieldValue(procedureCase, "propertyNumber"), FieldValue(procedureCase, "housePlotShopNumber"));
        var normalizedPropertyUnit = propertyUnit?.ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(normalizedPropertyUnit))
        {
            var matchingAsset = await _db.MaintenanceAssets
                .AsNoTracking()
                .Where(item => item.TenantId == tenantId && !item.IsDeleted)
                .Where(item =>
                    item.AssetNumber.ToLower() == normalizedPropertyUnit
                    || item.Name.ToLower() == normalizedPropertyUnit
                    || item.AssetNumber.ToLower().Contains(normalizedPropertyUnit)
                    || item.Name.ToLower().Contains(normalizedPropertyUnit))
                .OrderBy(item => item.AssetNumber)
                .FirstOrDefaultAsync();
            if (matchingAsset is not null)
            {
                return matchingAsset;
            }
        }

        return await _db.MaintenanceAssets
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted && item.Status == AssetStatus.Active)
            .OrderBy(item => item.AssetNumber)
            .FirstOrDefaultAsync()
            ?? await _db.MaintenanceAssets
                .AsNoTracking()
                .Where(item => item.TenantId == tenantId && !item.IsDeleted)
                .OrderBy(item => item.AssetNumber)
                .FirstOrDefaultAsync();
    }

    private async Task<ErpSystem.Core.Entities.Maintenance.MaintenanceType?> ResolveFacilitiesMaintenanceTypeAsync(
        ProcedureCase procedureCase,
        Guid tenantId)
    {
        var issueType = FirstNonBlank(FieldValue(procedureCase, "issueType"), FieldValue(procedureCase, "complaintCategory"));
        if (!string.IsNullOrWhiteSpace(issueType))
        {
            var normalizedIssueType = issueType.ToLowerInvariant();
            var matchingType = await _db.MaintenanceTypes
                .AsNoTracking()
                .Where(item => item.TenantId == tenantId && !item.IsDeleted && item.IsActive)
                .Where(item =>
                    item.Name.ToLower().Contains(normalizedIssueType)
                    || item.Code.ToLower().Contains(normalizedIssueType)
                    || item.Category.ToLower().Contains(normalizedIssueType))
                .OrderBy(item => item.SortOrder)
                .ThenBy(item => item.Name)
                .FirstOrDefaultAsync();
            if (matchingType is not null)
            {
                return matchingType;
            }
        }

        return await _db.MaintenanceTypes
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted && item.IsActive)
            .OrderByDescending(item => item.MaintenanceClass == "Corrective")
            .ThenBy(item => item.SortOrder)
            .ThenBy(item => item.Name)
            .FirstOrDefaultAsync();
    }

    private async Task<ErpSystem.Core.Entities.Maintenance.PriorityLevel?> ResolveFacilitiesPriorityLevelAsync(
        ProcedureCase procedureCase,
        Guid tenantId)
    {
        var priority = FieldValue(procedureCase, "priority")?.Trim();
        var desiredLevel = priority?.ToLowerInvariant() switch
        {
            "urgent" or "critical" or "emergency" => 1,
            "high" => 2,
            "normal" or "medium" => 3,
            "low" => 4,
            _ => 3
        };

        return await _db.PriorityLevels
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted && item.IsActive)
            .OrderBy(item => item.Level == desiredLevel ? 0 : 1)
            .ThenBy(item => item.Level)
            .FirstOrDefaultAsync();
    }

    private static DateTime? ParseProcedureDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed)
            ? parsed
            : null;
    }

    private async Task NotifyProcedureStageAssignedAsync(
        ProcedureCase procedureCase,
        string completedStageName,
        Guid userId,
        Guid tenantId)
    {
        if (string.IsNullOrWhiteSpace(procedureCase.CurrentStageName)
            || string.IsNullOrWhiteSpace(procedureCase.CurrentAssignedRole)
            || string.Equals(procedureCase.CurrentStageName, completedStageName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var assignedRoles = ResolveProcedureNotificationRoles(procedureCase);
        if (assignedRoles.Count == 0)
        {
            return;
        }

        var sourceReference = FirstNonBlank(procedureCase.ReferenceNumber, procedureCase.Title, procedureCase.Id.ToString()) ?? procedureCase.Id.ToString();
        var actionUrl = BuildProcedureCaseActionUrl(procedureCase);
        var moduleLabel = ProcedureModuleLabel(procedureCase.Module);
        var assignedLabel = string.Join(" / ", assignedRoles);

        await RoleNotificationDispatcher.NotifyRolesAsync(
            _db,
            _notificationService,
            tenantId,
            userId,
            assignedRoles,
            $"{moduleLabel} case assigned: {sourceReference}",
            $"{sourceReference} moved from '{completedStageName}' to '{procedureCase.CurrentStageName}' and is assigned to {assignedLabel}.",
            $"{NotificationTopicSegment(procedureCase.Module)}.procedure.stage-assigned",
            "ProcedureCase",
            procedureCase.Id,
            actionUrl,
            new Dictionary<string, object>
            {
                ["sourceLabel"] = $"Source: {moduleLabel} workflow",
                ["sourceModule"] = procedureCase.Module,
                ["sourceEntityType"] = procedureCase.EntityType,
                ["sourceRecordReference"] = sourceReference,
                ["completedStage"] = completedStageName,
                ["nextStage"] = procedureCase.CurrentStageName,
                ["assignedRole"] = assignedLabel,
                ["applicantName"] = procedureCase.ApplicantName ?? string.Empty
            },
            CancellationToken.None);

        _db.ProcedureCaseActivities.Add(Activity(
            tenantId,
            userId,
            procedureCase.Id,
            "Role notified",
            procedureCase.CurrentStageName,
            $"Notified {assignedLabel} for {moduleLabel} stage assignment."));

        await _db.SaveChangesAsync();
    }

    private async Task NotifyLegalTransferPaymentRequestedAsync(
        ProcedureCase legalCase,
        string completedStageName,
        Guid tenantId,
        Guid actorUserId,
        DateTime now)
    {
        if (!string.Equals(legalCase.EntityType, "LegalTransfer", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(completedStageName, "Head of Legal Minuting", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(legalCase.CurrentStageName, "Client Payment Call", StringComparison.OrdinalIgnoreCase)
            || !Guid.TryParse(FieldValue(legalCase, "sourceProcedureCaseId"), out var sourceCaseId))
        {
            return;
        }

        var sourceCase = await _db.ProcedureCases
            .AsNoTracking()
            .Include(item => item.Fields.Where(field => !field.IsDeleted))
            .FirstOrDefaultAsync(item => item.TenantId == tenantId
                && item.Id == sourceCaseId
                && !item.IsDeleted);
        if (sourceCase is null)
        {
            throw new InvalidOperationException("The source Estate property record for the Legal transfer was not found.");
        }

        var amount = ParseProcedureAmount(FieldValue(legalCase, "transferFeePayable"));
        var amountText = amount.HasValue
            ? $"GHS {amount.Value:N2}"
            : FieldValue(legalCase, "transferFeePayable") ?? "the minuted transfer fee";
        var legalReference = legalCase.ReferenceNumber ?? legalCase.Id.ToString();
        var paymentRequestReference = BuildLegalTransferFeePaymentRequestReference(legalCase);
        var sourceReference = sourceCase.ReferenceNumber ?? sourceCase.Title;
        var invoice = await CreateOrLinkLegalTransferFeeInvoiceAsync(
            legalCase,
            sourceCase,
            tenantId,
            paymentRequestReference,
            cancellationToken: CancellationToken.None);

        await UpsertLinkedSourceFieldAsync(tenantId, legalCase.Id, "paymentStatus", "Payment status", "Pending", actorUserId, now);
        await UpsertLinkedSourceFieldAsync(tenantId, legalCase.Id, "transferFeePaymentRequestReference", "Transfer fee payment request reference", paymentRequestReference, actorUserId, now);
        await UpsertLinkedSourceFieldAsync(tenantId, legalCase.Id, "transferFeeInvoiceId", "Transfer fee invoice ID", invoice.Id.ToString(), actorUserId, now);
        await UpsertLinkedSourceFieldAsync(tenantId, legalCase.Id, "transferFeeInvoiceReference", "Transfer fee invoice reference", invoice.InvoiceNumber, actorUserId, now);
        await UpsertLinkedSourceFieldAsync(tenantId, legalCase.Id, "transferFeeInvoiceStatus", "Transfer fee invoice status", invoice.Status, actorUserId, now);
        await UpsertLinkedSourceFieldAsync(tenantId, legalCase.Id, "transferFeeInvoiceAmount", "Transfer fee invoice amount", invoice.TotalAmount.ToString("0.00", CultureInfo.InvariantCulture), actorUserId, now);
        await UpsertLinkedSourceFieldAsync(tenantId, legalCase.Id, "transferFeeInvoicePaidAmount", "Transfer fee invoice paid amount", invoice.PaidAmount.ToString("0.00", CultureInfo.InvariantCulture), actorUserId, now);
        await UpsertLinkedSourceFieldAsync(tenantId, legalCase.Id, "transferFeeInvoiceBalance", "Transfer fee invoice balance", invoice.BalanceAmount.ToString("0.00", CultureInfo.InvariantCulture), actorUserId, now);
        await UpsertLinkedSourceFieldAsync(tenantId, legalCase.Id, "transferFeePaymentCheckStatus", "Transfer fee payment check", $"Awaiting customer payment; balance {invoice.CurrencyCode} {invoice.BalanceAmount:N2}", actorUserId, now);

        try
        {
            await RoleNotificationDispatcher.NotifyRolesAsync(
                _db,
                _notificationService,
                tenantId,
                actorUserId,
                ["Accounts Receivable Officer", "Finance Officer"],
                "Transfer fee invoice created",
                $"{legalReference}: Finance AR invoice {invoice.InvoiceNumber} was created for {sourceCase.ApplicantName ?? "the customer"} ({amountText}) using reference {paymentRequestReference}. Submit or send the invoice, then record and allocate the receipt when paid.",
                "legal.transfer-fee.invoice-created",
                "Invoice",
                invoice.Id,
                $"/finance/ar/invoices/{invoice.Id}",
                new Dictionary<string, object>
                {
                    ["sourceLabel"] = "Source: Legal -> Finance AR",
                    ["sourceModule"] = "Legal",
                    ["sourceEntityType"] = legalCase.EntityType,
                    ["sourceRecordReference"] = legalReference,
                    ["transferFeePayable"] = amountText,
                    ["paymentRequestReference"] = paymentRequestReference,
                    ["invoiceId"] = invoice.Id,
                    ["invoiceNumber"] = invoice.InvoiceNumber,
                    ["invoiceStatus"] = invoice.Status,
                    ["customerId"] = FieldValue(sourceCase, "sourceReference") ?? string.Empty,
                    ["customerName"] = sourceCase.ApplicantName ?? string.Empty
                },
                CancellationToken.None);

            _db.ProcedureCaseActivities.Add(Activity(
                tenantId,
                actorUserId,
                legalCase.Id,
                "Transfer fee invoice created",
                legalCase.CurrentStageName,
                $"Finance AR invoice {invoice.InvoiceNumber} created for {amountText}."));
        }
        catch
        {
            // Notification delivery must not block the Legal workflow stage transition.
        }

        await _db.SaveChangesAsync();
    }

    private async Task NotifyLegalTransferFeeInvoiceCustomerReadyAsync(
        ProcedureCase legalCase,
        Invoice invoice,
        Guid tenantId,
        Guid actorUserId,
        DateTime now)
    {
        if (!Guid.TryParse(FieldValue(legalCase, "sourceProcedureCaseId"), out var sourceCaseId)
            || string.Equals(FieldValue(legalCase, "transferFeeCustomerNotificationStatus"), "Notified", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var sourceCase = await _db.ProcedureCases
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId
                && item.Id == sourceCaseId
                && !item.IsDeleted);
        if (sourceCase is null || sourceCase.OpenedById == Guid.Empty)
        {
            return;
        }

        var transferFee = ParseProcedureAmount(FieldValue(legalCase, "transferFeePayable"));
        var amountText = transferFee.HasValue
            ? $"{invoice.CurrencyCode} {transferFee.Value:N2}"
            : $"{invoice.CurrencyCode} {invoice.TotalAmount:N2}";
        var legalReference = legalCase.ReferenceNumber ?? legalCase.Id.ToString();
        var sourceReference = sourceCase.ReferenceNumber ?? sourceCase.Title;
        var paymentRequestReference = BuildLegalTransferFeePaymentRequestReference(legalCase);

        try
        {
            await _notificationService.CreateNotificationAsync(
                new CreateNotificationDto
                {
                    RecipientId = sourceCase.OpenedById,
                    Type = "estate.property.transfer-fee-invoice-ready",
                    Title = "Transfer fee invoice ready",
                    Message = $"Finance AR invoice {invoice.InvoiceNumber} is approved and ready for payment. Pay {amountText} for Legal transfer {legalReference} using reference {paymentRequestReference}.",
                    Priority = "High",
                    EntityType = "Invoice",
                    EntityId = invoice.Id,
                    ActionUrl = "/external-portal/my-properties",
                    Metadata = new Dictionary<string, object>
                    {
                        ["legalCaseId"] = legalCase.Id,
                        ["legalReference"] = legalReference,
                        ["sourceReference"] = sourceReference,
                        ["transferFeePayable"] = amountText,
                        ["paymentRequestReference"] = paymentRequestReference,
                        ["invoiceId"] = invoice.Id,
                        ["invoiceNumber"] = invoice.InvoiceNumber,
                        ["invoiceStatus"] = invoice.Status.ToString()
                    }
                },
                actorUserId,
                tenantId);

            await UpsertLinkedSourceFieldAsync(tenantId, legalCase.Id, "transferFeeCustomerNotificationStatus", "Transfer fee customer notification status", "Notified", actorUserId, now);
            await UpsertLinkedSourceFieldAsync(tenantId, legalCase.Id, "transferFeeCustomerNotifiedAt", "Transfer fee customer notified at", now.ToString("O", CultureInfo.InvariantCulture), actorUserId, now);
            _db.ProcedureCaseActivities.Add(Activity(
                tenantId,
                actorUserId,
                legalCase.Id,
                "Customer transfer fee invoice notified",
                legalCase.CurrentStageName,
                $"Customer was notified that invoice {invoice.InvoiceNumber} is ready for payment."));
        }
        catch
        {
            // Customer notification should be retried by the next Finance payment sync instead of blocking Legal.
        }
    }

    private async Task NotifyLegalTransferDraftReadyForClientAsync(
        ProcedureCase legalCase,
        string completedStageName,
        Guid tenantId,
        Guid actorUserId,
        DateTime now)
    {
        if (!string.Equals(legalCase.EntityType, "LegalTransfer", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(completedStageName, "Legal Vetting", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(legalCase.CurrentStageName, "Client Execution", StringComparison.OrdinalIgnoreCase)
            || string.Equals(FieldValue(legalCase, "transferDraftCustomerNotificationStatus"), "Notified", StringComparison.OrdinalIgnoreCase)
            || !Guid.TryParse(FieldValue(legalCase, "sourceProcedureCaseId"), out var sourceCaseId))
        {
            return;
        }

        var sourceCase = await _db.ProcedureCases
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId
                && item.Id == sourceCaseId
                && !item.IsDeleted);
        if (sourceCase is null || sourceCase.OpenedById == Guid.Empty)
        {
            return;
        }

        var legalReference = legalCase.ReferenceNumber ?? legalCase.Id.ToString();
        var sourceReference = sourceCase.ReferenceNumber ?? sourceCase.Title;
        var draftReference = FirstNonBlank(FieldValue(legalCase, "draftDocumentReference"), FieldValue(legalCase, "agreementReference"));

        try
        {
            await _notificationService.CreateNotificationAsync(
                new CreateNotificationDto
                {
                    RecipientId = sourceCase.OpenedById,
                    Type = "estate.property.legal-transfer-draft-ready",
                    Title = "Transfer draft ready for signature",
                    Message = $"Legal transfer {legalReference} is ready. Download the draft, sign it, and upload the signed copy from My Properties.",
                    Priority = "High",
                    EntityType = "ProcedureCase",
                    EntityId = legalCase.Id,
                    ActionUrl = "/external-portal/my-properties",
                    Metadata = new Dictionary<string, object>
                    {
                        ["legalCaseId"] = legalCase.Id,
                        ["legalReference"] = legalReference,
                        ["sourceReference"] = sourceReference,
                        ["draftReference"] = draftReference ?? string.Empty,
                        ["currentStage"] = legalCase.CurrentStageName ?? string.Empty
                    }
                },
                actorUserId,
                tenantId);

            await UpsertLinkedSourceFieldAsync(tenantId, legalCase.Id, "transferDraftCustomerNotificationStatus", "Transfer draft customer notification status", "Notified", actorUserId, now);
            await UpsertLinkedSourceFieldAsync(tenantId, legalCase.Id, "transferDraftCustomerNotifiedAt", "Transfer draft customer notified at", now.ToString("O", CultureInfo.InvariantCulture), actorUserId, now);
            _db.ProcedureCaseActivities.Add(Activity(
                tenantId,
                actorUserId,
                legalCase.Id,
                "Customer transfer draft notified",
                legalCase.CurrentStageName,
                $"Customer was notified to sign and upload transfer draft {draftReference ?? legalReference}."));
            await _db.SaveChangesAsync();
        }
        catch
        {
            // Notification delivery must not block the Legal workflow stage transition.
        }
    }

    private async Task NotifyLegalTransferInterviewDateChangedAsync(
        ProcedureCase legalCase,
        string? previousInterviewDate,
        Guid tenantId,
        Guid actorUserId,
        DateTime now)
    {
        if (!string.Equals(legalCase.EntityType, "LegalTransfer", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(legalCase.CurrentStageName, "Client Execution", StringComparison.OrdinalIgnoreCase)
            || !Guid.TryParse(FieldValue(legalCase, "sourceProcedureCaseId"), out var sourceCaseId))
        {
            return;
        }

        var interviewDate = NormalizeProcedureDate(FieldValue(legalCase, "interviewDate"));
        if (string.IsNullOrWhiteSpace(interviewDate)
            || string.Equals(interviewDate, NormalizeProcedureDate(previousInterviewDate), StringComparison.OrdinalIgnoreCase)
            || string.Equals(interviewDate, NormalizeProcedureDate(FieldValue(legalCase, "transferInterviewCustomerNotifiedDate")), StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var sourceCase = await _db.ProcedureCases
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId
                && item.Id == sourceCaseId
                && !item.IsDeleted);
        if (sourceCase is null || sourceCase.OpenedById == Guid.Empty)
        {
            return;
        }

        var legalReference = legalCase.ReferenceNumber ?? legalCase.Id.ToString();
        var sourceReference = sourceCase.ReferenceNumber ?? sourceCase.Title;
        var displayDate = FormatProcedureDateForDisplay(interviewDate);

        try
        {
            await _notificationService.CreateNotificationAsync(
                new CreateNotificationDto
                {
                    RecipientId = sourceCase.OpenedById,
                    Type = "estate.property.legal-transfer-interview-date",
                    Title = "Transfer interview date set",
                    Message = $"Legal transfer {legalReference} has an interview/execution date of {displayDate}. Track the transfer from My Properties.",
                    Priority = "High",
                    EntityType = "ProcedureCase",
                    EntityId = legalCase.Id,
                    ActionUrl = "/external-portal/my-properties",
                    Metadata = new Dictionary<string, object>
                    {
                        ["legalCaseId"] = legalCase.Id,
                        ["legalReference"] = legalReference,
                        ["sourceReference"] = sourceReference,
                        ["interviewDate"] = interviewDate
                    }
                },
                actorUserId,
                tenantId);

            await UpsertLinkedSourceFieldAsync(tenantId, legalCase.Id, "transferInterviewCustomerNotifiedDate", "Transfer interview customer notified date", interviewDate, actorUserId, now);
            await UpsertLinkedSourceFieldAsync(tenantId, legalCase.Id, "transferInterviewCustomerNotifiedAt", "Transfer interview customer notified at", now.ToString("O", CultureInfo.InvariantCulture), actorUserId, now);
            _db.ProcedureCaseActivities.Add(Activity(
                tenantId,
                actorUserId,
                legalCase.Id,
                "Customer transfer interview notified",
                legalCase.CurrentStageName,
                $"Customer was notified of transfer interview/execution date {displayDate}."));
            await _db.SaveChangesAsync();
        }
        catch
        {
            // Notification delivery must not block saving Legal transfer intake updates.
        }
    }

    private static string? NormalizeProcedureDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return DateTime.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed)
            ? parsed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : trimmed;
    }

    private static string FormatProcedureDateForDisplay(string value)
    {
        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed)
            ? parsed.ToString("dd MMM yyyy", CultureInfo.InvariantCulture)
            : value;
    }

    private async Task<LegalTransferFeeInvoiceSnapshot> CreateOrLinkLegalTransferFeeInvoiceAsync(
        ProcedureCase legalCase,
        ProcedureCase sourceCase,
        Guid tenantId,
        string paymentRequestReference,
        CancellationToken cancellationToken)
    {
        var existingInvoice = await FindLegalTransferFeeInvoiceAsync(legalCase, tenantId);
        if (existingInvoice is not null)
        {
            return LegalTransferFeeInvoiceSnapshot.From(existingInvoice);
        }

        if (!Guid.TryParse(FieldValue(sourceCase, "sourceReference"), out var customerId))
        {
            throw new InvalidOperationException("The source Estate property record is not linked to a Finance AR customer.");
        }

        var amount = ParseProcedureAmount(FieldValue(legalCase, "transferFeePayable"));
        if (!amount.HasValue || amount.Value <= 0m)
        {
            throw new InvalidOperationException("Enter the transfer fee payable before creating the transfer-fee invoice.");
        }

        var revenueAccount = await _db.Accounts
            .AsNoTracking()
            .Where(account => account.TenantId == tenantId
                && !account.IsDeleted
                && account.Status == AccountStatus.Active
                && account.AccountType == AccountType.Revenue
                && account.AllowDirectPosting
                && !account.IsControlAccount)
            .OrderBy(account => account.AccountCode == "4100" ? 0 : 1)
            .ThenBy(account => account.AccountCode)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Finance must configure an active directly-postable revenue account before Legal transfer fee invoices can be created.");

        var legalReference = FirstNonBlank(legalCase.ReferenceNumber, legalCase.Id.ToString()) ?? legalCase.Id.ToString();
        var propertyNumber = FirstNonBlank(FieldValue(legalCase, "propertyNumber"), FieldValue(sourceCase, "propertyUnit"), FieldValue(sourceCase, "listingReference"));
        var currencyCode = FirstNonBlank(FieldValue(sourceCase, "currency"), "GHS")!;
        var invoice = await _invoiceService.CreateAsync(
            new InvoiceCreateDto
                {
                    CustomerId = customerId,
                    InvoiceDate = DateTime.UtcNow.Date,
                    DueDate = DateTime.UtcNow.Date,
                    Reference = paymentRequestReference,
                    CurrencyCode = currencyCode.Length == 3 ? currencyCode : "GHS",
                    ExchangeRate = 1m,
                    Notes = $"Legal transfer fee for {legalReference}; source {sourceCase.ReferenceNumber ?? sourceCase.Title}. Source: Legal -> Finance AR.",
                    LineItems =
                    [
                        new InvoiceLineItemCreateDto
                        {
                            LineItemType = "GLAccount",
                            GLAccountId = revenueAccount.Id,
                            Description = string.IsNullOrWhiteSpace(propertyNumber)
                                ? $"Legal transfer fee: {legalReference}"
                                : $"Legal transfer fee: {propertyNumber}",
                            Quantity = 1m,
                            UnitPrice = amount.Value,
                            TaxTreatment = TaxTreatment.Exempt,
                            DiscountPercentage = 0m
                        }
                    ]
                },
            cancellationToken);

        return LegalTransferFeeInvoiceSnapshot.From(invoice);
    }

    private async Task NotifyLegalTransferPaymentConfirmedAsync(
        ProcedureCase legalCase,
        bool wasPaymentReady,
        Guid tenantId,
        Guid actorUserId,
        DateTime now)
    {
        if (wasPaymentReady || !IsLegalTransferPaymentReady(legalCase))
        {
            return;
        }

        const string notificationType = "legal.transfer-fee.payment-confirmed";
        var alreadyNotified = await _db.Notifications
            .AsNoTracking()
            .AnyAsync(notification => notification.TenantId == tenantId
                && !notification.IsDeleted
                && notification.NotificationType == notificationType
                && notification.EntityType == "ProcedureCase"
                && notification.EntityId == legalCase.Id);
        if (alreadyNotified)
        {
            return;
        }

        var receiptReference = FirstNonBlank(
            FieldValue(legalCase, "paymentReceiptReference"),
            FieldValue(legalCase, "transferFeeReceipt"),
            legalCase.Documents.FirstOrDefault(document =>
                !document.IsDeleted
                && string.Equals(document.Name, "Transfer fee payment receipt", StringComparison.OrdinalIgnoreCase)
                && (!string.IsNullOrWhiteSpace(document.FileUrl) || !string.IsNullOrWhiteSpace(document.FileName)))?.FileName);
        var legalReference = FirstNonBlank(legalCase.ReferenceNumber, legalCase.Title, legalCase.Id.ToString())
            ?? legalCase.Id.ToString();
        var assignedRole = FirstNonBlank(legalCase.CurrentAssignedRole, "Legal Admin Assistant");

        try
        {
            var count = await RoleNotificationDispatcher.NotifyRolesAsync(
                _db,
                _notificationService,
                tenantId,
                actorUserId,
                [assignedRole],
                "Transfer fee payment confirmed",
                $"{legalReference}: transfer fee payment is confirmed. Continue Legal transfer processing.",
                notificationType,
                "ProcedureCase",
                legalCase.Id,
                $"/legal/{Uri.EscapeDataString(legalCase.EntityType)}?caseId={legalCase.Id}",
                new Dictionary<string, object>
                {
                    ["sourceLabel"] = "Source: Finance / Client -> Legal",
                    ["sourceModule"] = "Legal",
                    ["sourceEntityType"] = legalCase.EntityType,
                    ["sourceRecordReference"] = legalReference,
                    ["currentStage"] = legalCase.CurrentStageName,
                    ["assignedRole"] = assignedRole ?? string.Empty,
                    ["paymentStatus"] = FieldValue(legalCase, "paymentStatus") ?? string.Empty,
                    ["receiptReference"] = receiptReference ?? string.Empty
                },
                CancellationToken.None);

            if (count > 0)
            {
                _db.ProcedureCaseActivities.Add(Activity(
                    tenantId,
                    actorUserId,
                    legalCase.Id,
                    "Legal payment confirmation notified",
                    legalCase.CurrentStageName,
                    $"Notified {assignedRole} that transfer fee payment is confirmed."));

                await _db.SaveChangesAsync();
            }
        }
        catch
        {
            // Notification delivery must not block saving the transfer fee payment confirmation.
        }
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

    private async Task<Invoice?> FindLegalTransferFeeInvoiceAsync(ProcedureCase legalCase, Guid tenantId)
    {
        if (Guid.TryParse(FieldValue(legalCase, "transferFeeInvoiceId"), out var linkedInvoiceId))
        {
            var linkedInvoice = await _db.Invoices
                .AsNoTracking()
                .FirstOrDefaultAsync(invoice => invoice.TenantId == tenantId
                    && invoice.Id == linkedInvoiceId
                    && !invoice.IsDeleted);
            if (linkedInvoice is not null)
            {
                return linkedInvoice;
            }
        }

        var paymentRequestReference = BuildLegalTransferFeePaymentRequestReference(legalCase);
        return await _db.Invoices
            .AsNoTracking()
            .Where(invoice => invoice.TenantId == tenantId
                && !invoice.IsDeleted
                && invoice.Reference == paymentRequestReference)
            .OrderByDescending(invoice => invoice.CreatedAt)
            .FirstOrDefaultAsync();
    }

    private static bool IsCustomerVisibleInvoiceStatus(InvoiceStatus status) =>
        status is InvoiceStatus.Sent
            or InvoiceStatus.PartiallyPaid
            or InvoiceStatus.Paid
            or InvoiceStatus.Overdue
            or InvoiceStatus.Approved;

    private static bool AmountsMatch(decimal invoiceAmount, decimal transferFeePayable)
        => Math.Abs(invoiceAmount - transferFeePayable) < 0.01m;

    private static string BuildLegalTransferFeePaymentRequestReference(ProcedureCase legalCase)
    {
        var legalReference = FirstNonBlank(legalCase.ReferenceNumber, legalCase.Id.ToString()) ?? legalCase.Id.ToString();
        return BuildLegalTransferFeeInvoiceReference(legalReference);
    }

    private static string BuildLegalTransferFeeInvoiceReference(string legalReference)
    {
        var reference = $"LEGAL-TRANSFER-FEE-{legalReference}";
        return reference.Length <= 100 ? reference : reference[..100];
    }

    private sealed record LegalTransferFeeInvoiceSnapshot(
        Guid Id,
        string InvoiceNumber,
        string Status,
        decimal TotalAmount,
        decimal PaidAmount,
        decimal BalanceAmount,
        string CurrencyCode)
    {
        public static LegalTransferFeeInvoiceSnapshot From(Invoice invoice) => new(
            invoice.Id,
            invoice.InvoiceNumber,
            invoice.Status.ToString(),
            invoice.TotalAmount,
            invoice.PaidAmount,
            invoice.BalanceAmount,
            invoice.CurrencyCode);

        public static LegalTransferFeeInvoiceSnapshot From(InvoiceDto invoice) => new(
            invoice.Id,
            invoice.InvoiceNumber,
            invoice.Status,
            invoice.TotalAmount,
            invoice.PaidAmount,
            invoice.BalanceAmount,
            invoice.CurrencyCode);
    }

    private static bool IsLegalTransferClientPaymentStage(ProcedureCase procedureCase) =>
        string.Equals(procedureCase.EntityType, "LegalTransfer", StringComparison.OrdinalIgnoreCase)
        && string.Equals(procedureCase.CurrentStageName, "Client Payment Call", StringComparison.OrdinalIgnoreCase);

    private static bool HasLegalTransferFeeReceipt(ProcedureCase procedureCase) =>
        !string.IsNullOrWhiteSpace(FieldValue(procedureCase, "paymentReceiptReference"))
        || !string.IsNullOrWhiteSpace(FieldValue(procedureCase, "transferFeeReceipt"))
        || procedureCase.Documents.Any(document =>
            !document.IsDeleted
            && string.Equals(document.Name, "Transfer fee payment receipt", StringComparison.OrdinalIgnoreCase)
            && (!string.IsNullOrWhiteSpace(document.FileUrl) || !string.IsNullOrWhiteSpace(document.FileName)));

    private static bool IsLegalTransferPaymentReady(ProcedureCase procedureCase)
    {
        if (!IsLegalTransferClientPaymentStage(procedureCase))
        {
            return false;
        }

        return string.Equals(FieldValue(procedureCase, "paymentStatus")?.Trim(), "Paid", StringComparison.OrdinalIgnoreCase)
            && HasLegalTransferFeeReceipt(procedureCase);
    }

    private static decimal? ParseProcedureAmount(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var cleaned = new string(value.Where(character =>
            char.IsDigit(character) || character is '.' or '-').ToArray());
        return decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount)
            ? amount
            : null;
    }

    private static void EnsureLegalTransferHeadMinutingReady(ProcedureCase procedureCase)
    {
        if (!string.Equals(procedureCase.EntityType, "LegalTransfer", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(procedureCase.CurrentStageName, "Head of Legal Minuting", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var transferFee = ParseProcedureAmount(FieldValue(procedureCase, "transferFeePayable"));
        if (!transferFee.HasValue || transferFee.Value <= 0m)
        {
            throw new InvalidOperationException("Enter the transfer fee payable before submitting Head of Legal Minuting.");
        }
    }

    private static void EnsureLegalTransferClientPaymentReady(ProcedureCase procedureCase)
    {
        if (!IsLegalTransferClientPaymentStage(procedureCase))
        {
            return;
        }

        if (!string.Equals(FieldValue(procedureCase, "paymentStatus")?.Trim(), "Paid", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Finance must create and fully settle the transfer-fee AR invoice before submitting Client Payment Call.");
        }

        if (!HasLegalTransferFeeReceipt(procedureCase))
        {
            throw new InvalidOperationException("Refresh Finance payment after the customer receipt is allocated to the transfer-fee invoice.");
        }
    }

    private static void EnsureLegalTransferRequiredStageFieldsReady(ProcedureCase procedureCase)
    {
        if (!string.Equals(procedureCase.EntityType, "LegalTransfer", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var missing = procedureCase.CurrentStageName switch
        {
            "Head of Legal Minuting" => LegalTransferRequirementFailures(
                procedureCase,
                ("assignedLegalOfficer", HasProcedureValue, "Assign the Legal Officer before submitting."),
                ("transferFeePayable", HasPositiveProcedureAmount, "Enter a transfer fee payable greater than zero.")),
            "Transfer Drafting" => LegalTransferRequirementFailures(
                procedureCase,
                ("draftDocumentReference", HasProcedureValue, "Generate the transfer draft before submitting.")),
            "Legal Vetting" => LegalTransferRequirementFailures(
                procedureCase,
                ("dueDiligenceStatus", ValueIs("Cleared"), "Set due diligence status to Cleared."),
                ("cadastralPlanStatus", ValueIs("Available", "Not required"), "Set cadastral plan status to Available or Not required."),
                ("scheduleStatus", ValueIs("Inserted", "Not required"), "Set schedule insertion status to Inserted or Not required."),
                ("legalVettingStatus", ValueIs("Approved"), "Set legal vetting status to Approved.")),
            "Client Execution" => LegalTransferRequirementFailures(
                procedureCase,
                ("interviewDate", HasProcedureValue, "Enter the applicant / transferee interview date."),
                ("signatureStatus", ValueIs("Client signed", "Fully signed"), "Set signature status to Client signed.")),
            "Legal Officer Signature" => LegalTransferRequirementFailures(
                procedureCase,
                ("signatureStatus", ValueIs("Legal signed", "Fully signed"), "Sign the executed transfer form before submitting.")),
            "Legal Admin Signature" => LegalTransferRequirementFailures(
                procedureCase,
                ("sealStatus", ValueIs("Sealed", "Dated", "Sealed and dated"), "Set seal / dating status before submitting.")),
            "Head of Legal Signature" => LegalTransferRequirementFailures(
                procedureCase,
                ("signatureStatus", ValueIs("Head of Legal signed", "Fully signed"), "Set signature status to Head of Legal signed.")),
            "Legal Admin Closeout" => LegalTransferRequirementFailures(
                procedureCase,
                ("distributionStatus", ValueIs("Distributed", "Returned to Estate Records"), "Set signed transfer distribution status."),
                ("estateReturnStatus", ValueIs("Returned to Estate"), "Set Estate file return status to Returned to Estate.")),
            _ => []
        };

        if (missing.Count > 0)
        {
            throw new InvalidOperationException(missing[0]);
        }
    }

    private static List<string> LegalTransferRequirementFailures(
        ProcedureCase procedureCase,
        params (string Key, Func<string?, bool> IsSatisfied, string Message)[] requirements)
        => requirements
            .Where(requirement => !requirement.IsSatisfied(FieldValue(procedureCase, requirement.Key)))
            .Select(requirement => requirement.Message)
            .ToList();

    private static bool HasProcedureValue(string? value)
        => !string.IsNullOrWhiteSpace(value);

    private static bool HasPositiveProcedureAmount(string? value)
        => ParseProcedureAmount(value) is > 0m;

    private static Func<string?, bool> ValueIs(params string[] allowedValues)
        => value => allowedValues.Any(allowed => string.Equals(value?.Trim(), allowed, StringComparison.OrdinalIgnoreCase));

    private static bool IsExternalListingApplication(ProcedureCase procedureCase) =>
        string.Equals(procedureCase.SourceDepartment, "External Portal - Estate Listings", StringComparison.OrdinalIgnoreCase)
        && string.Equals(procedureCase.EntityType, "EstatePropertyManagementListingApplication", StringComparison.OrdinalIgnoreCase);

    private static bool HasFirstInternalStageBeenRoutedForward(ProcedureCase procedureCase)
        => IsExternalListingApplication(procedureCase) && procedureCase.CurrentStageIndex > 0;

    private static void EnsureExternalListingApprovalIsReady(ProcedureCase procedureCase)
    {
        if (!IsExternalListingApplication(procedureCase)
            || !IsApprovedDecision(FieldValue(procedureCase, "decisionStatus")))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(FieldValue(procedureCase, "generatedAgreementReference")))
        {
            throw new InvalidOperationException("Generate the agreement before completing final approval.");
        }

        if (!IsLegalAgreementReviewApproved(FieldValue(procedureCase, "legalAgreementReviewStatus")))
        {
            throw new InvalidOperationException("Legal must approve the generated agreement before completing final approval.");
        }

        if (!IsRentalListingApplication(procedureCase))
        {
            return;
        }

        var moveInDate = FieldValue(procedureCase, "moveInDate");
        if (string.IsNullOrWhiteSpace(moveInDate)
            || !DateOnly.TryParseExact(
                moveInDate.Trim(),
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out _))
        {
            throw new InvalidOperationException(
                "Set a valid approved move-in date before completing rental approval.");
        }
    }

    private static bool IsApprovedDecision(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim();
        return normalized.Equals("Approved", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("Approved ", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLegalAgreementReviewApproved(string? value)
        => !string.IsNullOrWhiteSpace(value)
            && value.Contains("approved", StringComparison.OrdinalIgnoreCase);

    private async Task AttachPropertyLegalHandoffDocumentsAsync(
        ProcedureCase legalCase,
        ProcedureCase sourceCase,
        LinkedLegalMatter matter,
        Guid tenantId,
        Guid userId,
        DateTime now)
    {
        await AttachGeneratedAgreementDocumentAsync(legalCase, sourceCase, matter, tenantId, userId, now);

        if (matter.Purpose == "ConveyanceRegistration")
        {
            NormalizeLegalTransferDocuments(legalCase, tenantId, userId, now);
            await AttachConveyanceTransferFileAsync(legalCase, sourceCase, tenantId, userId, now);
        }
    }

    private static bool IsProcedureDocumentSatisfied(ProcedureCase procedureCase, ProcedureCaseDocument document)
    {
        if (!string.IsNullOrWhiteSpace(document.FileUrl))
        {
            return true;
        }

        if (string.Equals(procedureCase.EntityType, "LegalTransfer", StringComparison.OrdinalIgnoreCase)
            && string.Equals(procedureCase.CurrentStageName, "Client Payment Call", StringComparison.OrdinalIgnoreCase)
            && string.Equals(document.Name, "Transfer fee payment receipt", StringComparison.OrdinalIgnoreCase))
        {
            return !string.IsNullOrWhiteSpace(FieldValue(procedureCase, "paymentReceiptReference"))
                || !string.IsNullOrWhiteSpace(FieldValue(procedureCase, "transferFeeReceipt"));
        }

        return false;
    }

    private static bool IsProcedureDocumentMandatoryForSubmission(ProcedureCase procedureCase, ProcedureCaseDocument document)
    {
        if (!document.IsMandatory)
        {
            return false;
        }

        return !(
            string.Equals(procedureCase.EntityType, "LegalTransfer", StringComparison.OrdinalIgnoreCase)
            && string.Equals(procedureCase.CurrentStageName, "Head of Legal Signature", StringComparison.OrdinalIgnoreCase)
            && string.Equals(document.Name, "Signed transfer distribution / Estate return note", StringComparison.OrdinalIgnoreCase));
    }

    private static (string StageName, string Provider)? ResolveLegalTransferDocumentStageAndProvider(string documentName)
    {
        var match = LegalTransferDocumentStages.FirstOrDefault(item =>
            string.Equals(item.Name, documentName, StringComparison.OrdinalIgnoreCase));

        return string.IsNullOrWhiteSpace(match.Name)
            ? null
            : (match.StageName, match.Provider);
    }

    private static void NormalizeLegalTransferDocuments(ProcedureCase legalCase, Guid tenantId, Guid userId, DateTime now)
    {
        if (!string.Equals(legalCase.EntityType, "LegalTransfer", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        foreach (var document in legalCase.Documents.Where(item => !item.IsDeleted))
        {
            var stageAndProvider = ResolveLegalTransferDocumentStageAndProvider(document.Name);
            if (stageAndProvider is null)
            {
                continue;
            }

            document.RequiredFrom = stageAndProvider.Value.StageName;
            document.ProvidedBy = stageAndProvider.Value.Provider;
            document.TenantId = tenantId;
            document.UpdatedAt = now;
            document.LastModifiedById = userId;
        }

        foreach (var (name, stageName, provider) in LegalTransferDocumentStages)
        {
            if (legalCase.Documents.Any(item => !item.IsDeleted && string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            legalCase.Documents.Add(new ProcedureCaseDocument
            {
                TenantId = tenantId,
                ProcedureCaseId = legalCase.Id,
                Name = name,
                RequiredFrom = stageName,
                ProvidedBy = provider,
                IsMandatory = true,
                CreatedById = userId,
                CreatedAt = now
            });
        }
    }

    private async Task AttachConveyanceTransferFileAsync(
        ProcedureCase legalCase,
        ProcedureCase sourceCase,
        Guid tenantId,
        Guid userId,
        DateTime now)
    {
        var sourceDocument = sourceCase.Documents
            .Where(document => !document.IsDeleted && !string.IsNullOrWhiteSpace(document.FileName))
            .OrderByDescending(document => string.Equals(document.Name, "Signed property agreement", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(document => document.Name.Contains("agreement", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(document => document.UploadedAt ?? document.UpdatedAt ?? document.CreatedAt)
            .FirstOrDefault();

        var agreementReference = FirstNonBlank(
            FieldValue(sourceCase, "finalSignedAgreementReference"),
            FieldValue(sourceCase, "signedAgreementReference"),
            FieldValue(sourceCase, "generatedAgreementReference"));
        var dmsRecord = string.IsNullOrWhiteSpace(agreementReference)
            ? null
            : await _db.CentralDocumentRecords
                .AsNoTracking()
                .Where(item => item.TenantId == tenantId
                    && !item.IsDeleted
                    && item.DocumentReference == agreementReference)
                .OrderByDescending(item => item.UpdatedAt ?? item.CreatedAt)
                .FirstOrDefaultAsync();

        if (sourceDocument is null && dmsRecord is null && string.IsNullOrWhiteSpace(agreementReference))
        {
            return;
        }

        var document = legalCase.Documents.FirstOrDefault(item =>
            !item.IsDeleted
            && string.Equals(item.Name, "Transfer file from Estate", StringComparison.OrdinalIgnoreCase));

        if (document is null)
        {
            document = new ProcedureCaseDocument
            {
                TenantId = tenantId,
                ProcedureCaseId = legalCase.Id,
                Name = "Transfer file from Estate",
                RequiredFrom = "Head of Legal Minuting",
                ProvidedBy = "Estate / Property Management",
                IsMandatory = true,
                CreatedById = userId,
                CreatedAt = now
            };
            legalCase.Documents.Add(document);
        }

        document.FileName = FirstNonBlank(sourceDocument?.FileName, dmsRecord?.Title, agreementReference, sourceCase.ReferenceNumber)
            ?? "Estate transfer file";
        document.FileUrl = FirstNonBlank(
            sourceDocument?.FileUrl,
            dmsRecord is not null ? $"/document-management/records/{dmsRecord.Id}" : null);
        document.Notes = $"Ported from Estate property record {sourceCase.ReferenceNumber ?? sourceCase.Id.ToString()}. Agreement reference: {agreementReference ?? document.FileName}.";
        document.RequiredFrom = "Head of Legal Minuting";
        document.ProvidedBy = "Estate / Property Management";
        document.UploadedById = userId;
        document.UploadedAt = now;
        document.UpdatedAt = now;
        document.LastModifiedById = userId;
    }

    private async Task AttachGeneratedAgreementDocumentAsync(
        ProcedureCase legalCase,
        ProcedureCase sourceCase,
        LinkedLegalMatter matter,
        Guid tenantId,
        Guid userId,
        DateTime now)
    {
        if (!matter.RequiresGeneratedAgreement)
        {
            return;
        }

        var agreementReference = FieldValue(sourceCase, "generatedAgreementReference")?.Trim();
        if (string.IsNullOrWhiteSpace(agreementReference))
        {
            return;
        }

        var dmsRecord = await _db.CentralDocumentRecords
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId
                && !item.IsDeleted
                && item.DocumentReference == agreementReference)
            .OrderByDescending(item => item.UpdatedAt ?? item.CreatedAt)
            .FirstOrDefaultAsync();

        var document = legalCase.Documents.FirstOrDefault(item =>
            !item.IsDeleted
            && string.Equals(item.Name, "Generated draft agreement", StringComparison.OrdinalIgnoreCase));

        if (document is null)
        {
            document = new ProcedureCaseDocument
            {
                TenantId = tenantId,
                ProcedureCaseId = legalCase.Id,
                Name = "Generated draft agreement",
                RequiredFrom = "Legal Intake",
                ProvidedBy = "Estate / Property Management",
                IsMandatory = true,
                CreatedById = userId,
                CreatedAt = now
            };
            legalCase.Documents.Add(document);
        }

        document.FileName = agreementReference;
        document.FileUrl = dmsRecord is not null
            ? $"/document-management/records/{dmsRecord.Id}"
            : $"/document-management?search={Uri.EscapeDataString(agreementReference)}";
        document.Notes = $"Generated agreement submitted from Property Management. DMS reference: {agreementReference}.";
        document.UploadedById = userId;
        document.UploadedAt = now;
        document.UpdatedAt = now;
        document.LastModifiedById = userId;
    }

    private static bool IsRentalListingApplication(ProcedureCase procedureCase)
    {
        var requestType = FieldValue(procedureCase, "requestType") ?? string.Empty;
        var title = procedureCase.Title ?? string.Empty;
        return !requestType.Contains("purchase", StringComparison.OrdinalIgnoreCase)
            && !requestType.Contains("sale", StringComparison.OrdinalIgnoreCase)
            && !title.StartsWith("Purchase bid", StringComparison.OrdinalIgnoreCase)
            && !title.StartsWith("Sale request", StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizeProcedureField(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim().ToLowerInvariant();

    private static bool IsPropertyManagementCase(ProcedureCase procedureCase)
        => string.Equals(procedureCase.Module, "PropertyManagement", StringComparison.OrdinalIgnoreCase)
            && string.Equals(procedureCase.EntityType, "EstatePropertyManagementListingApplication", StringComparison.OrdinalIgnoreCase);

    private bool CanInitiatePropertyLegalHandoff(ProcedureCase procedureCase)
        => IsWorkflowAdmin()
            || UserOwnsCase(procedureCase)
            || CurrentUserRoleNames().Any(role =>
                string.Equals(role, "Property Manager", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "Property Officer", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "Estate Manager", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "Head of Estate", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "Executive Approver", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "Authorised Signatory", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "Managing Director", StringComparison.OrdinalIgnoreCase));

    private static LinkedLegalMatter ResolveLinkedLegalMatter(string matterType)
    {
        var normalized = matterType?.Trim().ToLowerInvariant() ?? string.Empty;
        return normalized switch
        {
            "agreementreview" => new("AgreementReview", "LegalPropertyAgreementReview", "Agreement legal review", null, false, true, false),
            "conveyanceregistration" => new("ConveyanceRegistration", "LegalTransfer", "Conveyance and registration", "Transfer", false, false, true),
            "leaserenewal" => new("LeaseRenewal", "LegalLeaseVariationRenewalSublease", "Lease renewal", "Renewal", true, false, true),
            "leasevariation" => new("LeaseVariation", "LegalLeaseVariationRenewalSublease", "Deed of variation", "Deed of Variation", true, false, true),
            "sublease" => new("Sublease", "LegalLeaseVariationRenewalSublease", "Sublease", "Sublease", true, false, true),
            "assignment" => new("Assignment", "LegalAssignmentSubleaseVesting", "Assignment / vesting", "Assignment", true, false, true),
            "termination" => new("Termination", "LegalTerminationRecognition", "Lease termination / recognition", null, true, false, true),
            "mortgage" => new("Mortgage", "LegalMortgage", "Consent to mortgage", "Consent to Mortgage", true, false, true),
            "mortgageinprinciple" => new("MortgageInPrinciple", "LegalMortgageInPrinciple", "Mortgage in principle", "Mortgage in Principle", true, false, false),
            "courtprocess" => new("CourtProcess", "LegalCourtProcess", "Court process", null, true, false, false),
            "othercourtprocess" => new("OtherCourtProcess", "LegalOtherCourtProcess", "Other court process", null, true, false, false),
            "disputeadvisory" => new("DisputeAdvisory", "LegalOpinionAdvisory", "Property dispute / legal advisory", null, true, false, false),
            _ => throw new InvalidOperationException("Select a supported Legal matter type.")
        };
    }

    private static void UpsertLinkedSourceField(
        ProcedureCase sourceCase,
        string key,
        string label,
        string? value,
        Guid userId,
        DateTime now)
    {
        var field = sourceCase.Fields.FirstOrDefault(item =>
            !item.IsDeleted && string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase));
        if (field is null)
        {
            sourceCase.Fields.Add(new ProcedureCaseField
            {
                TenantId = sourceCase.TenantId,
                Key = key,
                Label = label,
                FieldType = "text",
                Value = value,
                CreatedById = userId,
                CreatedAt = now,
                UpdatedAt = now
            });
            return;
        }

        field.Value = value;
        field.LastModifiedById = userId;
        field.UpdatedAt = now;
    }

    private Task LinkSourceCaseToLegalMatterAsync(
        ProcedureCase sourceCase,
        ProcedureCaseDetailDto legalCase,
        LinkedLegalMatter matter,
        Guid tenantId,
        Guid userId,
        DateTime now) =>
        LinkSourceCaseToLegalMatterAsync(
            sourceCase,
            legalCase.Id,
            legalCase.ReferenceNumber,
            legalCase.CurrentStageName,
            matter,
            tenantId,
            userId,
            now);

    private Task LinkSourceCaseToLegalMatterAsync(
        ProcedureCase sourceCase,
        ProcedureCase legalCase,
        LinkedLegalMatter matter,
        Guid tenantId,
        Guid userId,
        DateTime now) =>
        LinkSourceCaseToLegalMatterAsync(
            sourceCase,
            legalCase.Id,
            legalCase.ReferenceNumber,
            legalCase.CurrentStageName,
            matter,
            tenantId,
            userId,
            now);

    private async Task LinkSourceCaseToLegalMatterAsync(
        ProcedureCase sourceCase,
        Guid legalCaseId,
        string? legalReference,
        string legalStageName,
        LinkedLegalMatter matter,
        Guid tenantId,
        Guid userId,
        DateTime now)
    {
        var openStatus = $"Open - {legalStageName}";
        await UpsertLinkedSourceFieldAsync(tenantId, sourceCase.Id, "legalLastMatterCaseId", "Latest linked Legal matter ID", legalCaseId.ToString(), userId, now);
        await UpsertLinkedSourceFieldAsync(tenantId, sourceCase.Id, "legalLastMatterReference", "Latest linked Legal matter reference", legalReference, userId, now);
        await UpsertLinkedSourceFieldAsync(tenantId, sourceCase.Id, "legalLastMatterType", "Latest linked Legal matter type", matter.Title, userId, now);
        await UpsertLinkedSourceFieldAsync(tenantId, sourceCase.Id, "legalLastMatterStatus", "Latest linked Legal matter status", openStatus, userId, now);

        if (matter.Purpose == "AgreementReview")
        {
            await UpsertLinkedSourceFieldAsync(tenantId, sourceCase.Id, "legalAgreementReviewCaseId", "Legal agreement review case ID", legalCaseId.ToString(), userId, now);
            await UpsertLinkedSourceFieldAsync(tenantId, sourceCase.Id, "legalAgreementReviewReference", "Legal agreement review reference", legalReference, userId, now);
            await UpsertLinkedSourceFieldAsync(tenantId, sourceCase.Id, "legalAgreementReviewStatus", "Legal agreement review status", $"Under Legal review - {legalStageName}", userId, now);
        }
        else if (matter.Purpose == "ConveyanceRegistration")
        {
            await UpsertLinkedSourceFieldAsync(tenantId, sourceCase.Id, "legalConveyanceCaseId", "Legal conveyance case ID", legalCaseId.ToString(), userId, now);
            await UpsertLinkedSourceFieldAsync(tenantId, sourceCase.Id, "legalConveyanceReference", "Legal conveyance reference", legalReference, userId, now);
            await UpsertLinkedSourceFieldAsync(tenantId, sourceCase.Id, "legalConveyanceStatus", "Legal conveyance / registration status", openStatus, userId, now);
            await UpsertLinkedSourceFieldAsync(tenantId, sourceCase.Id, "ownershipTransferStatus", "Ownership transfer status", "Blocked - Legal conveyance and registration pending", userId, now);
        }

        await _db.ProcedureCases
            .IgnoreQueryFilters()
            .Where(item => item.TenantId == tenantId
                && item.Id == sourceCase.Id
                && !item.IsDeleted)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.LastActionById, userId)
                .SetProperty(item => item.UpdatedAt, now));

        _db.ProcedureCaseActivities.Add(Activity(
            tenantId,
            userId,
            sourceCase.Id,
            "Lodged with Legal",
            sourceCase.CurrentStageName,
            $"{matter.Title} opened as {legalReference}."));
        await _db.SaveChangesAsync();
    }

    private async Task UpsertLinkedSourceFieldAsync(
        Guid tenantId,
        Guid sourceCaseId,
        string key,
        string label,
        string? value,
        Guid userId,
        DateTime now)
    {
        var trackedField = _db.ChangeTracker
            .Entries<ProcedureCaseField>()
            .Select(entry => entry.Entity)
            .FirstOrDefault(field => field.TenantId == tenantId
                && field.ProcedureCaseId == sourceCaseId
                && string.Equals(field.Key, key, StringComparison.OrdinalIgnoreCase));
        if (trackedField is not null)
        {
            trackedField.Label = label;
            trackedField.FieldType = "text";
            trackedField.Value = value;
            trackedField.IsDeleted = false;
            trackedField.DeletedAt = null;
            trackedField.DeletedBy = null;
            trackedField.LastModifiedById = userId;
            trackedField.UpdatedAt = now;
            return;
        }

        var updated = await _db.ProcedureCaseFields
            .IgnoreQueryFilters()
            .Where(field => field.TenantId == tenantId
                && field.ProcedureCaseId == sourceCaseId
                && field.Key == key)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(field => field.Label, label)
                .SetProperty(field => field.FieldType, "text")
                .SetProperty(field => field.Value, value)
                .SetProperty(field => field.IsDeleted, false)
                .SetProperty(field => field.DeletedAt, (DateTime?)null)
                .SetProperty(field => field.DeletedBy, (string?)null)
                .SetProperty(field => field.LastModifiedById, userId)
                .SetProperty(field => field.UpdatedAt, now));

        if (updated > 0)
        {
            return;
        }

        _db.ProcedureCaseFields.Add(new ProcedureCaseField
        {
            TenantId = tenantId,
            ProcedureCaseId = sourceCaseId,
            Key = key,
            Label = label,
            FieldType = "text",
            Value = value,
            CreatedById = userId,
            CreatedAt = now,
            UpdatedAt = now
        });
    }

    private static string? FirstNonBlank(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private async Task<ProcedureCase?> LoadCaseAsync(Guid id, bool asTracking)
    {
        var tenantId = RequireTenantId();
        var query = _db.ProcedureCases
            .AsSplitQuery()
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

    private async Task ArchiveCompetingExternalListingRequestsAsync(
        ProcedureCase selectedCase,
        Guid tenantId,
        Guid userId,
        DateTime now)
    {
        if (!IsExternalListingApplication(selectedCase))
        {
            return;
        }

        var listingReference = NormalizeProcedureField(FieldValue(selectedCase, "listingReference"));
        var propertyUnit = NormalizeProcedureField(FieldValue(selectedCase, "propertyUnit"));
        if (listingReference is null && propertyUnit is null)
        {
            return;
        }

        var competingCases = await _db.ProcedureCases
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId
                && item.Id != selectedCase.Id
                && !item.IsDeleted
                && item.SourceDepartment == "External Portal - Estate Listings"
                && item.EntityType == "EstatePropertyManagementListingApplication"
                && item.Status != "Archived"
                && item.Status != "Completed"
                && item.Status != "Cancelled"
                && item.Status != "Canceled"
                && ((listingReference != null && item.Fields.Any(field =>
                        !field.IsDeleted &&
                        field.Key == "listingReference" &&
                        field.Value != null &&
                        field.Value.Trim().ToLower() == listingReference))
                    || (propertyUnit != null && item.Fields.Any(field =>
                        !field.IsDeleted &&
                        field.Key == "propertyUnit" &&
                        field.Value != null &&
                        field.Value.Trim().ToLower() == propertyUnit))))
            .Select(item => new
            {
                item.Id,
                item.CurrentStageName,
                item.ReferenceNumber
            })
            .ToListAsync();

        if (competingCases.Count == 0)
        {
            return;
        }

        var competingIds = competingCases.Select(item => item.Id).ToList();
        await _db.ProcedureCases
            .IgnoreQueryFilters()
            .Where(item => item.TenantId == tenantId && competingIds.Contains(item.Id) && !item.IsDeleted)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Status, "Archived")
                .SetProperty(item => item.CurrentStageName, "Archived")
                .SetProperty(item => item.CurrentStageOwner, (string?)null)
                .SetProperty(item => item.CurrentAssignedRole, (string?)null)
                .SetProperty(item => item.LastActionById, userId)
                .SetProperty(item => item.UpdatedAt, now));

        await _db.ProcedureCaseFields
            .IgnoreQueryFilters()
            .Where(field => field.TenantId == tenantId
                && competingIds.Contains(field.ProcedureCaseId)
                && !field.IsDeleted
                && field.Key == "applicationStatus")
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(field => field.Value, "Archived - another request routed")
                .SetProperty(field => field.UpdatedAt, now));

        var selectedReference = selectedCase.ReferenceNumber ?? selectedCase.Id.ToString();
        var listingLabel = FieldValue(selectedCase, "listingReference")
            ?? FieldValue(selectedCase, "propertyUnit")
            ?? "the listing";
        foreach (var competingCase in competingCases)
        {
            _db.ProcedureCaseActivities.Add(Activity(
                tenantId,
                userId,
                competingCase.Id,
                "Archived competing request",
                competingCase.CurrentStageName,
                $"Archived because request {selectedReference} was routed for {listingLabel}."));
        }

        await _db.SaveChangesAsync();
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
            "DocumentManagement" => BuildDocumentManagementSeed(entityType),
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
                    ResolveStepAssignmentLabel(step, null) ?? step.Name,
                    step.Id,
                    BuildConfiguredWorkflowChecklist(step))
                {
                    WorkflowDefinitionId = workflow.Id,
                    WorkflowDefinitionName = workflow.Name
                })
                .ToList();
        }

        if (string.Equals(module, "Facilities", StringComparison.OrdinalIgnoreCase)
            && string.Equals(entityType, "EstateFacilityMaintenance", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Facilities Maintenance requires an active published workflow in Administration > Workflow Setup.");
        }

        if (string.Equals(
                entityType,
                "EstatePropertyManagementListingApplication",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Property Requests / Listing Applications requires an active published workflow in Administration > Workflow Setup.");
        }

        return module switch
        {
            "Legal" => _legalCatalog.GetProcedureWorkspace(entityType)?.Stages
                .Select((stage, index) => new StageSeed(index, stage.Name, stage.Owner, stage.Owner, null, stage.Checklist))
                .ToList() ?? [],
            "Estate" => [],
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

    private static IReadOnlyList<string> BuildConfiguredWorkflowChecklist(WorkflowStep step)
    {
        var configuration = DeserializeStepConfiguration(step.Configuration);
        return configuration?.QualityConfig?.QualityChecks
            .Where(check => check.IsRequired && !string.IsNullOrWhiteSpace(check.Name))
            .Select(check => check.Name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];
    }

    private (string Title, IReadOnlyList<FieldSeed> Fields, IReadOnlyList<DocumentSeed> Documents) BuildLegalSeed(string entityType)
    {
        var workspace = _legalCatalog.GetProcedureWorkspace(entityType)
            ?? throw new InvalidOperationException($"Legal procedure workspace '{entityType}' was not found.");

        return (
            workspace.Procedure.Title,
            workspace.IntakeFields.Select(item => new FieldSeed(item.Key, item.Label, item.Type, item.Options)).ToList(),
            BuildLegalDocumentSeeds(entityType, workspace));
    }

    private static IReadOnlyList<DocumentSeed> BuildLegalDocumentSeeds(string entityType, LegalProcedureWorkspace workspace)
    {
        if (string.Equals(entityType, "LegalTransfer", StringComparison.OrdinalIgnoreCase))
        {
            return workspace.RequiredDocuments
                .Select(item =>
                {
                    var stageAndProvider = ResolveLegalTransferDocumentStageAndProvider(item.Name);
                    return stageAndProvider is null
                        ? new DocumentSeed(item.Name, item.RequiredFrom, item.IsMandatory)
                        : new DocumentSeed(item.Name, stageAndProvider.Value.StageName, item.IsMandatory, stageAndProvider.Value.Provider);
                })
                .ToList();
        }

        return workspace.RequiredDocuments
            .Select(item => new DocumentSeed(item.Name, item.RequiredFrom, item.IsMandatory))
            .ToList();
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

    private FieldSeed? ResolveProcedureFieldSeed(ProcedureCase procedureCase, string key)
    {
        try
        {
            var seed = procedureCase.Module switch
            {
                "Legal" => BuildLegalSeed(procedureCase.EntityType).Fields,
                "Facilities" => BuildFacilitiesSeed(procedureCase.EntityType).Fields,
                "PropertyManagement" => BuildPropertyManagementSeed(procedureCase.EntityType).Fields,
                "Planning" => BuildPlanningSeed(procedureCase.EntityType).Fields,
                "Estate" => BuildEstateFieldSeeds(GetEstateProcedure(procedureCase.EntityType)),
                _ => []
            };

            return seed.FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return null;
        }
    }

    private static string ToProcedureFieldLabel(string key)
    {
        var cleaned = key.Trim();
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return "Field";
        }

        var label = new List<char>(cleaned.Length + 4);
        for (var index = 0; index < cleaned.Length; index++)
        {
            var current = cleaned[index];
            if (current is '_' or '-')
            {
                label.Add(' ');
                continue;
            }

            if (index > 0
                && char.IsUpper(current)
                && cleaned[index - 1] != ' '
                && !char.IsUpper(cleaned[index - 1]))
            {
                label.Add(' ');
            }

            label.Add(current);
        }

        var result = new string(label.ToArray()).Trim();
        return result.Length == 0
            ? "Field"
            : char.ToUpperInvariant(result[0]) + result[1..];
    }

    private (string Title, IReadOnlyList<FieldSeed> Fields, IReadOnlyList<DocumentSeed> Documents) BuildEstateSeed(string entityType)
    {
        var procedure = GetEstateProcedure(entityType);

        return (
            procedure.Title,
            [],
            []);
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
        => [];

    private static IReadOnlyList<FieldSeed> BuildEstateFieldSeeds(EstateProcedureCatalogItem procedure)
    {
        var fields = new List<FieldSeed>
        {
            new("referenceNumber", "Reference number", "text", null),
            new("procedureType", "Procedure", "text", [procedure.Title]),
            new("applicantName", "Applicant / lessee / client name", "text", null),
            new("portalRecipientIdentity", "Portal recipient email / username", "text", null),
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
            new("sopSectionReference", "SOP section reference", "text", null),
            new("approvedFeeScheduleReference", "Approved fee / appendix reference", "text", null),
            new("approvedRateReference", "Approved rate reference", "text", null),
            new("documentTemplateReference", "Approved form / template reference", "text", null),
            new("feeReference", "Fee / invoice / receipt reference", "text", null),
            new("legalReference", "Legal reference", "text", null),
            new("financeReference", "Finance reference", "text", null),
            new("planningReference", "Planning / site plan reference", "text", null),
            new("dmsFolderReference", "DMS folder reference", "text", null),
            new("financeHandoffStatus", "Finance handoff status", "select", ["Not required", "Pending invoice", "Invoice raised", "Receipt confirmed", "Returned for correction"]),
            new("legalHandoffStatus", "Legal handoff status", "select", ["Not required", "Pending legal review", "Sent to Legal", "Legal completed", "Returned for correction"]),
            new("recordsHandoffStatus", "Records handoff status", "select", ["Not required", "Pending Records update", "Sent to Records", "Records updated", "Returned for correction"]),
            new("reportingReference", "Reporting / quarterly return reference", "text", null)
        };

        // Estate manual controls stay in Estate; linked teams receive source/reference fields without changing their modules.
        if (string.Equals(procedure.EntityType, "EstateRegistrySecretariat", StringComparison.OrdinalIgnoreCase))
        {
            fields.AddRange([
                new("intakeType", "Intake type", "select", ["Incoming file", "Outgoing letter", "Form purchase", "Typing request", "Client pickup", "Internal dispatch"]),
                new("registryBook", "Registry book", "select", ["General notebook", "Regularization notebook", "Kpone notebook", "Letters book", "Forms purchase book", "Movement register"]),
                new("formType", "Form type", "select", ["Estate Transfer Form", "House Ownership Scheme Form", "Rental Unit Form", "Other"]),
                new("formReceiptNumber", "Form purchase receipt number", "text", null),
                new("formIssuedTo", "Form issued to", "text", null),
                new("fileMovementReference", "File movement reference", "text", null),
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
                new("recordActionType", "Record action type", "select", ["Estate register update", "HOS ledger update", "Rent register update", "Transfer amendment", "Assignment amendment", "Rental-to-HOS conversion", "Agency notification", "Building permit ownership verification", "Invitation / mediation letter"]),
                new("sourceTransferCaseReference", "Source transfer / assignment case reference", "text", null),
                new("housePlotShopNumber", "House / plot / shop number", "text", null),
                new("registerReference", "Register / ledger reference", "text", null),
                new("oldLesseeName", "Previous lessee / tenant name", "text", null),
                new("newLesseeName", "New lessee / tenant name", "text", null),
                new("newLesseeAddress", "New lessee address", "textarea", null),
                new("transferEffectiveDate", "Transfer effective date", "date", null),
                new("transferDeclarationReference", "Transfer Declaration form reference", "text", null),
                new("voluntaryVacationReference", "Voluntary vacation of tenancy evidence reference", "text", null),
                new("hosFormReference", "House Ownership Scheme form reference", "text", null),
                new("tenantNamesChangingToHos", "Tenant names changing to HOS", "textarea", null),
                new("houseType", "House type", "text", null),
                new("purchaseAmount", "Purchase amount / amount bought", "currency", null),
                new("purchaseDate", "Date property was purchased", "date", null),
                new("estateRegisterUpdateStatus", "Estate register update status", "select", ["Not started", "Updated", "Returned for correction", "Not applicable"]),
                new("ledgerUpdateStatus", "Ledger update status", "select", ["Not started", "Rent ledger updated", "HOS ledger updated", "Both ledgers updated", "Returned for correction", "Not applicable"]),
                new("revenueRecordsUpdateStatus", "Revenue records update status", "select", ["Not started", "Revenue updated", "Estate Records updated", "Revenue and Estate Records updated", "Returned for correction"]),
                new("recordsAmendmentConfirmation", "Records amendment confirmation", "textarea", null),
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
                new("propertyFileReviewStatus", "Property-file review status", "select", ["Not started", "File reviewed", "File missing", "Returned for correction"]),
                new("groundRentArrearsCheckStatus", "Ground-rent arrears check status", "select", ["Not checked", "No arrears", "Arrears exist", "Waiver / exception approved"]),
                new("searchFeeReceipt", "Search fee receipt", "text", null),
                new("searchReportReference", "Search report reference", "text", null),
                new("searchCoverLetterReference", "Search cover letter reference", "text", null)
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
                new("transferProcessType", "Transfer process type", "select", ["Transfer of interest", "Portion transfer", "Assignment", "Joint ownership addition", "Rental transfer", "Rental-to-HOS conversion"]),
                new("housePlotShopNumber", "House / plot / shop number", "text", null),
                new("transferorName", "Transferor / assignor / existing lessee", "text", null),
                new("transfereeName", "Transferee / assignee / incoming party", "text", null),
                new("newLesseeAddress", "New lessee / transferee address", "textarea", null),
                new("transferEffectiveDate", "Transfer effective date", "date", null),
                new("transferDeclarationReference", "Transfer Declaration form reference", "text", null),
                new("transferorDeclarationStatus", "Transferor declaration status", "select", ["Not checked", "Completed and signed", "Missing signature", "Returned for correction"]),
                new("transfereeDeclarationStatus", "Transferee declaration status", "select", ["Not checked", "Completed and signed", "Missing signature", "Returned for correction"]),
                new("voluntaryVacationReference", "Voluntary vacation of tenancy evidence reference", "text", null),
                new("hosFormReference", "HOS form reference when rental changes to HOS", "text", null),
                new("houseType", "House type when HOS applies", "text", null),
                new("purchaseAmount", "Purchase amount / amount bought", "currency", null),
                new("considerationAmount", "Consideration amount", "currency", null),
                new("transferFeePayable", "Transfer / assignment fee payable", "currency", null),
                new("revenueRecordsReference", "Revenue records amendment reference", "text", null),
                new("estateRecordsReference", "Estate Records amendment reference", "text", null),
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
                new("landUse", "Land use", "select", ["Residential", "Commercial", "Institutional", "Industrial", "Agro Industrial", "Fuel Station", "Mixed Use", "Other"]),
                new("leaseTermYears", "Lease term (years)", "number", null),
                new("leaseCommencementDate", "Lease commencement / move-in date", "date", null),
                new("groundRentPayable", "Ground Rent payable", "currency", null),
                new("paymentFrequency", "Payment frequency", "select", ["Annual", "SemiAnnual", "Quarterly", "Monthly"]),
                new("developmentStatus", "Development status", "select", ["Not checked", "Undeveloped", "Partially developed", "Substantially developed", "Completed"]),
                new("buildingPermitReference", "Building permit reference", "text", null),
                new("leasePreparationFee", "Lease preparation fee", "currency", null),
                new("cadastralInvoiceReference", "Cadastral invoice reference", "text", null),
                new("cadastralFeeReceiptReference", "Cadastral fee receipt reference", "text", null),
                new("leaseRequestFormReference", "Lease request form reference", "text", null),
                new("legalLeasePreparationStatus", "Legal lease preparation status", "select", ["Not sent", "Sent to Legal", "Legal drafting", "Registered lease returned", "Detached to Records"]),
                new("registeredLeaseReference", "Registered lease reference", "text", null),
                new("detachmentReference", "Legal detachment / Records update reference", "text", null)
            ]);
        }

        if (string.Equals(procedure.EntityType, "EstateLeaseRenewal", StringComparison.OrdinalIgnoreCase))
        {
            fields.AddRange([
                new("originalLeaseReference", "Original lease reference", "text", null),
                new("variationReason", "Variation / renewal reason", "textarea", null),
                new("existingLeaseExpiryDate", "Existing lease expiry date", "date", null),
                new("yearsToExpiry", "Years to expiry", "number", null),
                new("unexpiredTermBand", "Unexpired term band", "select", ["Not assessed", "10 years or less", "More than 10 years"]),
                new("surrenderOptionStatus", "Surrender option status", "select", ["Not required", "Surrender requested", "Surrender accepted", "Surrender rejected"]),
                new("developmentStatus", "Development proposal / status", "text", null),
                new("renewalPremium", "Renewal premium", "currency", null),
                new("improvedGroundRent", "Improved Ground Rent", "currency", null),
                new("lrtcReference", "LRTC reference", "text", null),
                new("committeeDecision", "LRTC decision", "select", ["Pending", "Approved", "Returned", "Rejected"]),
                new("deedOfVariationReference", "Deed of Variation reference", "text", null)
            ]);
        }

        if (string.Equals(procedure.EntityType, "EstateAdditionalLand", StringComparison.OrdinalIgnoreCase))
        {
            fields.AddRange([
                new("adjoiningPlotNumber", "Adjoining plot number", "text", null),
                new("additionalLandSizeAcres", "Additional land size (acres)", "number", null),
                new("availabilityStatus", "Availability status", "select", ["Not checked", "Available", "Unavailable", "Disputed", "Requires layout revision"]),
                new("feeRecommendationReference", "Fee recommendation reference", "text", null),
                new("recommendation", "Estate recommendation", "textarea", null),
                new("approvalDecision", "Approval decision", "select", ["Pending", "Approved", "Returned", "Rejected"]),
                new("offerOrRefusalReference", "Offer / refusal letter reference", "text", null)
            ]);
        }

        if (string.Equals(procedure.EntityType, "EstateLayoutRevision", StringComparison.OrdinalIgnoreCase))
        {
            fields.AddRange([
                new("existingLayoutReference", "Existing layout reference", "text", null),
                new("proposedLayoutReference", "Proposed layout reference", "text", null),
                new("revisionReason", "Revision reason", "textarea", null),
                new("planningComment", "Planning comment", "textarea", null),
                new("mdApprovalReference", "MD approval reference", "text", null),
                new("layoutRevisionDispatchReference", "Dispatch / Records update reference", "text", null)
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
                new("rateRevisionBasis", "Rate revision basis", "textarea", null),
                new("mdSignatureReference", "MD / HOE signature reference", "text", null),
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
                new("communityRegularised", "Community / area being regularised", "text", null),
                new("applicationFormReference", "Completed application form reference", "text", null),
                new("revenueRecordsConfirmation", "Revenue and Estate Records confirmation", "text", null),
                new("invitationLetterReference", "Invitation letter reference", "text", null),
                new("interviewDate", "Interview date", "date", null),
                new("committeeVettingReference", "Committee vetting reference", "text", null)
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
                new("rightOfEntryReference", "Right of Entry reference", "text", null),
                new("rightOfEntryIssuedDate", "Right of Entry issued date", "date", null),
                new("quarterlyReportReference", "Quarterly report reference", "text", null)
            ]);
        }

        if (string.Equals(procedure.EntityType, "EstateHousingHomeOwnership", StringComparison.OrdinalIgnoreCase))
        {
            fields.AddRange([
                new("housingRequestType", "Housing request type", "select", ["Recognition of tenancy", "Rental transfer", "Conversion to HOS", "Purchase completion", "Rental offer", "Lease request"]),
                new("houseType", "House type", "text", null),
                new("unitNumber", "Unit / house number", "text", null),
                new("declarationReference", "Statutory declaration reference", "text", null),
                new("hosFormReference", "House Ownership Scheme form reference", "text", null),
                new("tenantNamesChangingToHos", "Tenant names changing to HOS", "textarea", null),
                new("rentCardNumber", "Rent card number", "text", null),
                new("rentRegisterReference", "Rent register reference", "text", null),
                new("sellingPrice", "Selling price", "currency", null),
                new("purchaseAmount", "Purchase amount / amount bought", "currency", null),
                new("purchaseDate", "Date property was purchased", "date", null),
                new("paymentCompletionStatus", "Payment completion status", "select", ["Not checked", "Deposit paid", "Arrears cleared", "Full selling price paid", "Payment incomplete"]),
                new("dateOfTenancy", "Date of tenancy", "date", null),
                new("ledgerUpdateStatus", "HOS / rent ledger update status", "select", ["Not started", "Rent ledger updated", "HOS ledger updated", "Both ledgers updated", "Returned for correction"]),
                new("recordsUpdateReference", "Revenue and Estate Records update reference", "text", null)
            ]);
        }

        if (string.Equals(procedure.EntityType, "EstateTraditionalLands", StringComparison.OrdinalIgnoreCase))
        {
            fields.AddRange([
                new("traditionalCouncil", "Traditional Council / Stool", "select", ["Tema Manhean", "Nungua", "Kpone", "Other"]),
                new("allocationLetterReference", "Traditional allocation letter reference", "text", null),
                new("sitePlanReference", "Traditional site plan reference", "text", null),
                new("priorAllocationStatus", "Prior allocation status", "select", ["Not checked", "No prior allocation", "Prior allocation found", "Disputed", "Undefined signatories"]),
                new("proposalLetterReference", "Proposal letter reference", "text", null),
                new("offerLetterReference", "Offer Letter reference", "text", null),
                new("rightOfEntryReference", "Right of Entry reference", "text", null),
                new("quarterlyReportReference", "Quarterly report reference", "text", null),
                new("rejectionReason", "Rejection reason", "textarea", null)
            ]);
        }

        if (string.Equals(procedure.EntityType, "EstateReportingControls", StringComparison.OrdinalIgnoreCase))
        {
            fields.AddRange([
                new("reportType", "Report type", "select", ["Quarterly productivity", "Rent roll", "Debtor list", "Allocation report", "Transfer and assignment report", "Lease and mortgage report", "Control exception register", "Board summary"]),
                new("reportingPeriod", "Reporting period", "text", null),
                new("sourceSchedule", "Source schedule", "select", ["Records", "Serviced Plots", "Lands / Partially Serviced", "Housing", "Traditional Lands", "Regularisation", "Facilities", "Property Management", "All Estate"]),
                new("applicationsReceived", "Applications received", "number", null),
                new("applicationsProcessed", "Applications processed", "number", null),
                new("expectedRevenue", "Expected revenue", "currency", null),
                new("paymentsReceived", "Payments received", "currency", null),
                new("debtorCount", "Debtor count", "number", null),
                new("transfersCompleted", "Transfers / assignments completed", "number", null),
                new("leasesOrMortgagesProcessed", "Leases / mortgages processed", "number", null),
                new("appendixFeeVersion", "Appendix fee version used", "text", null),
                new("boardSubmissionReference", "Board submission reference", "text", null),
                new("auditTrailReference", "Audit trail reference", "text", null),
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
            new("Approved SOP form or template reference", "Estate / Central DMS", false),
            new("Approved fee schedule or appendix extract", "Estate / Finance", false),
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
                new("Issued Estate Transfer / HOS / Rental form register", "Estate Registry", false),
                new("Letters book or dispatch entry", "Estate Registry", true),
                new("File movement trace", "Estate Registry", true),
                new("Typed letter or notice", "Estate Registry", false)
            ],
            "EstateRecordsManagement" => [
                new("Estate register or ledger extract", "Estate Records", true),
                new("Transfer Declaration form completed by transferor and transferee", "Transferor / Transferee", false),
                new("Voluntary vacation of tenancy evidence", "Applicant / Estate Records", false),
                new("House Ownership Scheme form for rental-to-HOS conversion", "Housing / Applicant", false),
                new("House type and purchase amount ledger schedule", "Housing / Estate Records", false),
                new("Revenue and Estate Records amendment confirmation", "Revenue / Estate Records", true),
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
                new("Ground rent arrears confirmation", "Finance / Revenue", true),
                new("Property file review extract", "Estate Records", true),
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
                new("Lease preparation fee receipt", "Finance / Revenue", true),
                new("Cadastral plan invoice and receipt", "Estate / Planning / Revenue", false),
                new("Lease request form to Legal", "Estate / Legal", true),
                new("Legal lease preparation tracking note", "Legal / Estate", false),
                new("Registered lease copy for detachment", "Legal / Lands Commission", false)
            ],
            "EstateMortgageConsent" => [
                new("Consent to mortgage / mortgage in principle application", "Applicant", true),
                new("Draft deed or mortgage document", "Applicant / Legal", true),
                new("Development and arrears verification", "Estate / Finance Revenue", true),
                new("Mortgage consent response", "Estate Department", true)
            ],
            "EstateTransfer" => [
                new("Transfer Declaration form completed by transferor and transferee", "Transferor / Transferee", true),
                new("Voluntary vacation of tenancy evidence", "Transferor / Estate Records", true),
                new("Transfer effective-date evidence", "Applicant / Legal / Estate Records", true),
                new("New lessee address evidence", "Transferee", true),
                new("Transfer fee payment confirmation", "Finance / Revenue", false),
                new("Revenue and Estate Records amendment confirmation", "Revenue / Estate Records", true),
                new("Registered transfer instrument or Legal completion note", "Legal / Lands Commission", false)
            ],
            "EstateAssignment" => [
                new("Assignment application / consent request", "Applicant", true),
                new("Draft deed or assignment document", "Applicant / Legal", true),
                new("Transfer Declaration or assignment party declaration", "Assignor / Assignee", true),
                new("New lessee or assignee address evidence", "Assignee", true),
                new("Revenue and Estate Records amendment confirmation", "Revenue / Estate Records", true),
                new("Registered assignment instrument or Legal completion note", "Legal / Lands Commission", false)
            ],
            "EstateAdditionalLand" => [
                new("Additional land application", "Applicant", true),
                new("Adjoining plot verification", "Estate Records", true),
                new("Inspection and availability report", "Estate / Planning", true),
                new("Fee recommendation and approved rate extract", "Estate / Finance", false),
                new("Approval recommendation", "HOE / MD", true),
                new("Offer or refusal letter", "Estate Department", true)
            ],
            "EstateLayoutRevision" => [
                new("Layout revision request", "Applicant / Estate", true),
                new("Existing and proposed layout plans", "Planning / Development", true),
                new("Layout revision letter", "Estate Department", true),
                new("MD signed approval", "MD / HOE", true),
                new("Dispatch and Records update evidence", "Registry / Estate Records", true)
            ],
            "EstateChangeOfUse" => [
                new("Change-of-use application", "Applicant", true),
                new("Site inspection report", "Estate / Planning", true),
                new("Existing and proposed land-use fee calculation worksheet", "Estate / Finance", true),
                new("Change-of-use fee calculation", "Estate Department", true),
                new("Approval or refusal letter", "HOE / MD", true),
                new("Payment confirmation", "Finance / Revenue", false)
            ],
            "EstateReminderRateRevision" => [
                new("Unpaid proposal / arrears schedule", "Estate / Revenue", true),
                new("Reminder or rate revision notice", "Estate Department", true),
                new("Approved revised-rate calculation", "Estate / Finance", true),
                new("Signed notice approval", "HOE / MD", true),
                new("Dispatch evidence", "Registry", true)
            ],
            "EstateLeaseRenewal" => [
                new("Lease renewal application", "Applicant", true),
                new("Unexpired-term assessment", "Estate", true),
                new("Lease Renewal Technical Committee approval", "LRTC", true),
                new("Renewal invoice / demand letter", "Estate / Finance Revenue", true),
                new("Surrender and renewal option evidence", "Estate / Legal", false),
                new("Deed of Variation draft", "Estate / Legal", true),
                new("Registered Deed of Variation / Records update evidence", "Legal / Estate Records", false)
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
                new("Application form or application letter", "Applicant / Registry", true),
                new("Proposal Letter with LMF and Ground Rent", "Estate Lands / Partially Serviced", true),
                new("LMF and Ground Rent calculation worksheet", "Estate Lands / Finance", true),
                new("Offer Letter", "Estate Lands / Partially Serviced", true),
                new("Right of Entry", "Estate Lands / Partially Serviced", true),
                new("Quarterly lands schedule report extract", "Estate Lands / Partially Serviced", false)
            ],
            "EstateHousingHomeOwnership" => [
                new("Recognition or HOS application", "Applicant / Housing", true),
                new("Rental Transfer Form", "Housing Section", false),
                new("Rent Card", "Housing / MD", false),
                new("House Ownership Scheme form for rental-to-HOS conversion", "Housing / Applicant", true),
                new("Tenant names changing to HOS schedule", "Housing Section", true),
                new("House type and selling price / purchase amount schedule", "Housing / Estate Records", true),
                new("HOS Offer Letter", "Housing Section", true),
                new("Payment completion evidence", "Revenue / Housing", false),
                new("HOS ledger and Estate Records update evidence", "Revenue / Estate Records", true),
                new("Lease request for purchased house", "Housing / Legal", false)
            ],
            "EstateTraditionalLands" => [
                new("Traditional Council allocation letter", "Traditional Council", true),
                new("Traditional Council site plan", "Traditional Council / Planning", true),
                new("Proposal Letter with LMF and Ground Rent", "Estate Traditional Lands", true),
                new("LMF and Ground Rent calculation worksheet", "Estate Traditional Lands / Finance", true),
                new("Offer Letter", "Estate Traditional Lands", true),
                new("Right of Entry", "Estate Traditional Lands", true),
                new("Quarterly traditional lands report extract", "Estate Traditional Lands", false)
            ],
            "EstateTenancyRegularisation" => [
                new("Invitation letter", "Estate Regularisation", true),
                new("Regularisation requirements pack", "Applicant", true),
                new("Completed regularisation application form", "Applicant", true),
                new("Revenue and Estate Records confirmation", "Revenue / Estate Records", true),
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
                new("Transfer and assignment report", "Estate Records", false),
                new("Lease and mortgage report", "Estate / Legal", false),
                new("Approved appendix fee schedule control sheet", "Estate / Finance", true),
                new("Control exception register", "Estate Management", true),
                new("Board summary / approved report pack", "HOE / Estate Managers", true)
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

    private async Task<string?> SyncEstateGroundRentAssessmentAsync(
        ProcedureCase procedureCase,
        IDictionary<string, string?> requestedValues,
        Guid tenantId,
        Guid userId,
        DateTime now)
    {
        if (!string.Equals(procedureCase.Module, "Estate", StringComparison.OrdinalIgnoreCase)
            || !HasLandFeeDetermination(procedureCase.EntityType))
        {
            return null;
        }

        string? CurrentValue(string key)
        {
            if (requestedValues.TryGetValue(key, out var requestedValue))
            {
                return requestedValue;
            }

            return procedureCase.Fields
                .FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase))
                ?.Value;
        }

        var propertyNumber = CurrentValue("propertyNumber")?.Trim();
        if (string.IsNullOrWhiteSpace(propertyNumber)
            || !TryParseEstateDecimal(CurrentValue("plotSizeAcres"), out var plotSizeAcres)
            || !TryParseEstateDecimal(CurrentValue("groundRentRatePerAcre"), out var ratePerAcre)
            || plotSizeAcres <= 0
            || ratePerAcre < 0)
        {
            return null;
        }

        var asset = await _db.EstateManagedAssets
            .FirstOrDefaultAsync(item =>
                item.TenantId == tenantId
                && item.AssetCode == propertyNumber
                && !item.IsDeleted);
        if (asset == null)
        {
            return null;
        }

        var rawGroundRent = plotSizeAcres * ratePerAcre;
        var computed = decimal.Round(
            rawGroundRent,
            3,
            MidpointRounding.AwayFromZero);
        asset.GroundRentRatePerAcre = ratePerAcre;
        asset.GroundRentComputed = computed;
        asset.GroundRentPayable = decimal.Ceiling(rawGroundRent);
        asset.UpdatedAt = now;
        asset.UpdatedBy = _currentUser.UserName;
        asset.LastModifiedById = userId;

        return asset.AssetCode;
    }

    private static bool TryParseEstateDecimal(string? value, out decimal result)
        => decimal.TryParse(
            value,
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out result);

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
                && (taskConfig.RequiresDocument
                    || taskConfig.DocumentRequirements.Count > 0
                    || string.Equals(taskConfig.TaskActionType, "document", StringComparison.OrdinalIgnoreCase)))
            {
                var requirements = taskConfig.DocumentRequirements
                    .Where(requirement => !string.IsNullOrWhiteSpace(requirement.DocumentName))
                    .ToList();

                if (requirements.Count > 0)
                {
                    // Workflow setup: each configured stage document must become a ProcedureCaseDocument requirement.
                    documents.AddRange(requirements.Select(requirement =>
                        new DocumentSeed(
                            requirement.DocumentName.Trim(),
                            step.Name,
                            requirement.IsRequired,
                            requirement.ProvidedBy,
                            requirement.DocumentType,
                            requirement.AppliesTo)));
                }
                else
                {
                    var name = string.IsNullOrWhiteSpace(taskConfig.DocumentName)
                        ? $"{step.Name} document"
                        : taskConfig.DocumentName.Trim();
                    documents.Add(new DocumentSeed(name, step.Name, true));
                }
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
        if (IsCompleted(procedureCase))
        {
            throw new InvalidOperationException("Completed cases cannot be changed.");
        }

        if (!CanEdit(procedureCase))
        {
            throw new UnauthorizedAccessException("The current user is not assigned to this procedure stage.");
        }
    }

    private void EnsureCanCreateProcedureCase(string module)
    {
        if (!string.Equals(module, "Legal", StringComparison.OrdinalIgnoreCase) || CanCreateLegalProcedureCase())
        {
            return;
        }

        throw new UnauthorizedAccessException("The current user is not allowed to open Legal procedure cases.");
    }

    private bool CanEdit(ProcedureCase procedureCase)
    {
        if (IsCompleted(procedureCase))
        {
            return false;
        }

        if (IsWorkflowAdmin())
        {
            return true;
        }

        return UserHasAssignedProcedureRole(procedureCase);
    }

    private static (string Title, IReadOnlyList<FieldSeed> Fields, IReadOnlyList<DocumentSeed> Documents)
        BuildDocumentManagementSeed(string entityType)
    {
        if (!string.Equals(entityType, "CentralDocumentVersion", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Document Management procedure workspace '{entityType}' is not supported.");
        }

        return (
            "Document version approval",
            [
                new FieldSeed("documentRecordId", "DMS record ID", "text", []),
                new FieldSeed("documentVersionId", "Working version ID", "text", []),
                new FieldSeed("documentReference", "DMS reference", "text", []),
                new FieldSeed("versionNumber", "Version", "text", []),
                new FieldSeed("changeSummary", "Change summary", "textarea", [])
            ],
            []);
    }

    private static bool IsCompleted(ProcedureCase procedureCase)
        => procedureCase.Status.Trim().ToLowerInvariant() is
            "completed" or "archived" or "rejected" or "cancelled" or "canceled" or "closed";

    private static string? LegalTransferSignatureRoleForStage(string? stageName)
        => stageName switch
        {
            "Legal Officer Signature" => "Legal Officer",
            "Legal Admin Signature" => "Legal Admin Assistant",
            "Head of Legal Signature" => "Head of Legal",
            _ => null
        };

    private static bool IsLegalTransferStageSignatureRecorded(ProcedureCase procedureCase, ProcedureCaseDocument document)
    {
        var fileName = document.FileName ?? string.Empty;
        var notes = document.Notes ?? string.Empty;

        return procedureCase.CurrentStageName switch
        {
            "Legal Officer Signature" =>
                ContainsAny(fileName, "legal-officer-signed")
                || ContainsAny(notes, "Legal Officer signed from Legal transfer workspace")
                || ContainsAny(notes, "Legal Officer digitally signed"),
            "Legal Admin Signature" =>
                ContainsAny(fileName, "legal-admin-assistant-signed")
                || ContainsAny(notes, "Legal Admin Assistant signed from Legal transfer workspace")
                || ContainsAny(notes, "Legal Admin Assistant digitally signed"),
            "Head of Legal Signature" =>
                ContainsAny(fileName, "head-of-legal-signed")
                || ContainsAny(notes, "Head of Legal signed from Legal transfer workspace")
                || ContainsAny(notes, "Head of Legal digitally signed"),
            _ => false
        };
    }

    private static string BuildSignedProcedureDocumentFileName(string fileName, string signatureRole)
    {
        var safeRole = string.Join("-", signatureRole
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToLowerInvariant();
        var baseName = Path.GetFileNameWithoutExtension(fileName);
        if (string.IsNullOrWhiteSpace(baseName))
        {
            baseName = "executed-transfer-form";
        }

        return $"{baseName}-{safeRole}-signed.pdf";
    }

    private void UpsertProcedureCaseField(
        ProcedureCase procedureCase,
        IDictionary<string, ProcedureCaseField> fields,
        string key,
        string label,
        string fieldType,
        string? value,
        Guid actorId,
        DateTime now)
    {
        if (fields.TryGetValue(key, out var field))
        {
            field.Value = value;
            field.UpdatedAt = now;
            field.LastModifiedById = actorId;
            return;
        }

        var created = new ProcedureCaseField
        {
            TenantId = procedureCase.TenantId,
            ProcedureCaseId = procedureCase.Id,
            Key = key,
            Label = label,
            FieldType = fieldType,
            Value = value,
            CreatedAt = now,
            CreatedById = actorId
        };
        _db.ProcedureCaseFields.Add(created);
        fields[key] = created;
    }

    private bool CanCreateLegalProcedureCase()
        => IsWorkflowAdmin() || CurrentUserRoleNames().Any(role =>
            string.Equals(role, "Legal", StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, "Legal Officer", StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, "Legal Manager", StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, "Head of Legal", StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, "Legal Admin Assistant", StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, "Secretary", StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, "Registry", StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, "Legal Registry", StringComparison.OrdinalIgnoreCase));

    private bool CanView(ProcedureCase procedureCase)
        => IsWorkflowAdmin()
            || CanOverseePropertyListingApplication(procedureCase)
            || UserOwnsCase(procedureCase)
            || UserHasAssignedProcedureRole(procedureCase);

    private bool CanOverseePropertyListingApplication(ProcedureCase procedureCase)
        => string.Equals(
                procedureCase.EntityType,
                "EstatePropertyManagementListingApplication",
                StringComparison.OrdinalIgnoreCase)
            && CurrentUserRoleNames().Any(role =>
                string.Equals(role, "Property Manager", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "Estate Manager", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "Head of Estate", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "Executive Approver", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "Authorised Signatory", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "Managing Director", StringComparison.OrdinalIgnoreCase));

    private bool UserOwnsCase(ProcedureCase procedureCase)
        => Guid.TryParse(_currentUser.UserId, out var userId) && procedureCase.OpenedById == userId;

    private bool UserHasAssignedProcedureRole(ProcedureCase procedureCase)
    {
        if (string.IsNullOrWhiteSpace(procedureCase.CurrentAssignedRole))
        {
            return false;
        }

        var assignedTokens = SplitAssignedRoles(procedureCase.CurrentAssignedRole);
        if (Guid.TryParse(_currentUser.UserId, out var userId)
            && assignedTokens.Any(token => IsAssignedUserToken(token, userId)))
        {
            return true;
        }

        return CurrentUserRoleNames().Any(role =>
            assignedTokens.Any(token => string.Equals(token, role, StringComparison.OrdinalIgnoreCase)));
    }

    private static string[] SplitAssignedRoles(string assignedRole)
        => assignedRole
            .Split(['/', ',', ';', '|'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    private static IReadOnlyList<string> ResolveProcedureNotificationRoles(ProcedureCase procedureCase)
    {
        var currentStageName = procedureCase.CurrentStageName?.Trim();
        var roles = SplitAssignedRoles(procedureCase.CurrentAssignedRole ?? string.Empty)
            .Where(role => !role.StartsWith("User:", StringComparison.OrdinalIgnoreCase))
            .Where(role => !string.Equals(role, currentStageName, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (roles.Count == 0
            && !string.IsNullOrWhiteSpace(procedureCase.CurrentStageOwner)
            && !string.Equals(procedureCase.CurrentStageOwner, currentStageName, StringComparison.OrdinalIgnoreCase))
        {
            roles.Add(procedureCase.CurrentStageOwner.Trim());
        }

        return roles;
    }

    private static string BuildProcedureCaseActionUrl(ProcedureCase procedureCase)
    {
        var entityType = Uri.EscapeDataString(procedureCase.EntityType);
        var query = $"caseId={procedureCase.Id}";

        if (string.Equals(procedureCase.Module, "Legal", StringComparison.OrdinalIgnoreCase))
        {
            return $"/legal/{entityType}?{query}";
        }

        if (string.Equals(procedureCase.Module, "Facilities", StringComparison.OrdinalIgnoreCase))
        {
            return $"/estate/facilities/{entityType}?{query}";
        }

        if (string.Equals(procedureCase.Module, "PropertyManagement", StringComparison.OrdinalIgnoreCase))
        {
            return $"/estate/property-management/{entityType}?{query}";
        }

        if (string.Equals(procedureCase.Module, "Estate", StringComparison.OrdinalIgnoreCase))
        {
            return $"/estate/{entityType}?{query}";
        }

        return $"/dashboard?{query}";
    }

    private static string ProcedureModuleLabel(string? module)
        => module switch
        {
            not null when module.Equals("PropertyManagement", StringComparison.OrdinalIgnoreCase) => "Property Management",
            not null when module.Equals("Facilities", StringComparison.OrdinalIgnoreCase) => "Facilities",
            not null when module.Equals("Legal", StringComparison.OrdinalIgnoreCase) => "Legal",
            not null when module.Equals("Estate", StringComparison.OrdinalIgnoreCase) => "Estate",
            not null when !string.IsNullOrWhiteSpace(module) => module.Trim(),
            _ => "Workflow"
        };

    private static string NotificationTopicSegment(string? module)
    {
        var source = string.IsNullOrWhiteSpace(module) ? "workflow" : module.Trim();
        var normalized = new string(source
            .Select(character => char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '.')
            .ToArray());

        while (normalized.Contains("..", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("..", ".", StringComparison.Ordinal);
        }

        return normalized.Trim('.');
    }

    private static bool IsAssignedUserToken(string token, Guid userId)
    {
        const string UserPrefix = "User:";
        return token.StartsWith(UserPrefix, StringComparison.OrdinalIgnoreCase)
            && Guid.TryParse(token[UserPrefix.Length..].Trim(), out var assignedUserId)
            && assignedUserId == userId;
    }

    private static bool IsProcedureCaseContentUrl(string? fileUrl)
        => !string.IsNullOrWhiteSpace(fileUrl)
            && fileUrl.Replace('\\', '/').TrimStart('/')
                .StartsWith("api/procedure-cases/", StringComparison.OrdinalIgnoreCase);

    private bool IsWorkflowAdmin() =>
        CurrentUserRoleNames().Any(role =>
            string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, "SystemAdmin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, "SuperAdmin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, "TenantAdmin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, "WorkflowAdmin", StringComparison.OrdinalIgnoreCase));

    private IReadOnlyCollection<string> CurrentUserRoleNames()
    {
        if (_currentUserRoleNames is not null)
        {
            return _currentUserRoleNames;
        }

        var roles = _currentUser.Roles
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Select(role => role.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (Guid.TryParse(_currentUser.UserId, out var userId) && userId != Guid.Empty)
        {
            var databaseRoles = _db.UserRoles
                .AsNoTracking()
                .Where(userRole => userRole.UserId == userId)
                .Join(
                    _db.Roles.AsNoTracking(),
                    userRole => userRole.RoleId,
                    role => role.Id,
                    (_, role) => role.Name)
                .Where(role => !string.IsNullOrWhiteSpace(role))
                .ToList();

            foreach (var role in databaseRoles)
            {
                roles.Add(role!.Trim());
            }
        }

        _currentUserRoleNames = roles;
        return _currentUserRoleNames;
    }

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

        if (string.Equals(module, "DocumentManagement", StringComparison.OrdinalIgnoreCase)
            || string.Equals(module, "Document Management", StringComparison.OrdinalIgnoreCase)
            || string.Equals(module, "DMS", StringComparison.OrdinalIgnoreCase))
        {
            return "DocumentManagement";
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

    private async Task<ProcedureCaseDetailDto> ToDetailDtoAsync(ProcedureCase procedureCase)
    {
        var currentStageFieldKeys = await GetCurrentStageFieldKeysAsync(procedureCase);
        var fields = procedureCase.Fields.OrderBy(item => item.CreatedAt).Select(ToFieldDto).ToList();
        await AddFacilitiesMaintenanceExecutionStatusFieldsAsync(procedureCase, fields);
        return new(
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
            currentStageFieldKeys,
            fields,
            procedureCase.ChecklistItems.OrderBy(item => item.StageIndex).ThenBy(item => item.CreatedAt).Select(ToChecklistDto).ToList(),
            procedureCase.Documents.OrderBy(item => item.CreatedAt).Select(ToDocumentDto).ToList(),
            procedureCase.Activities.OrderByDescending(item => item.PerformedAt).Take(20).Select(ToActivityDto).ToList());
    }

    private async Task AddFacilitiesMaintenanceExecutionStatusFieldsAsync(
        ProcedureCase procedureCase,
        List<ProcedureCaseFieldDto> fields)
    {
        if (!string.Equals(procedureCase.Module, "Facilities", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(procedureCase.EntityType, "EstateFacilityMaintenance", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var jobCard = await ResolveFacilitiesMaintenanceJobCardAsync(procedureCase);
        if (jobCard is null)
        {
            return;
        }

        string? workOrderNumber = jobCard.GeneratedWorkOrderNumber;
        string? workOrderStatus = null;
        if (jobCard.GeneratedWorkOrderId.HasValue)
        {
            var workOrder = await _db.WorkOrders
                .AsNoTracking()
                .Where(item => item.TenantId == procedureCase.TenantId
                    && !item.IsDeleted
                    && item.Id == jobCard.GeneratedWorkOrderId.Value)
                .Select(item => new { item.WorkOrderNumber, item.Status })
                .FirstOrDefaultAsync();

            workOrderNumber = workOrder?.WorkOrderNumber ?? workOrderNumber;
            workOrderStatus = workOrder?.Status;
        }

        var jobCardComplete = IsMaintenanceExecutionComplete(jobCard.JobCardStatus);
        var workOrderComplete = !jobCard.GeneratedWorkOrderId.HasValue
            ? true
            : IsMaintenanceExecutionComplete(workOrderStatus);
        var executionComplete = jobCardComplete && workOrderComplete;
        var executionStatus = workOrderStatus is null
            ? $"Job card {jobCard.JobCardNumber} - {jobCard.JobCardStatus}"
            : $"Job card {jobCard.JobCardNumber} - {jobCard.JobCardStatus}; work order {workOrderNumber ?? jobCard.GeneratedWorkOrderNumber ?? "not recorded"} - {workOrderStatus}";

        AddOrReplaceSyntheticField(fields, "maintenanceJobCardStatus", "Maintenance job card status", jobCard.JobCardStatus);
        AddOrReplaceSyntheticField(fields, "maintenanceWorkOrderReference", "Maintenance work order reference", workOrderNumber);
        AddOrReplaceSyntheticField(fields, "maintenanceWorkOrderStatus", "Maintenance work order status", workOrderStatus);
        AddOrReplaceSyntheticField(fields, "maintenanceExecutionStatus", "Maintenance execution status", executionStatus);
        AddOrReplaceSyntheticField(fields, "maintenanceExecutionComplete", "Maintenance execution complete", executionComplete ? "true" : "false");
    }

    private static void AddOrReplaceSyntheticField(
        List<ProcedureCaseFieldDto> fields,
        string key,
        string label,
        string? value)
    {
        var existingIndex = fields.FindIndex(field => string.Equals(field.Key, key, StringComparison.OrdinalIgnoreCase));
        var field = new ProcedureCaseFieldDto(Guid.Empty, key, label, "text", value, null);
        if (existingIndex >= 0)
        {
            fields[existingIndex] = field;
            return;
        }

        fields.Add(field);
    }

    private async Task<IReadOnlyList<string>> GetCurrentStageFieldKeysAsync(ProcedureCase procedureCase)
    {
        if (!procedureCase.WorkflowStepId.HasValue)
        {
            return [];
        }

        var configurationJson = await _db.WorkflowSteps
            .AsNoTracking()
            .Where(step => step.Id == procedureCase.WorkflowStepId.Value && !step.IsDeleted)
            .Select(step => step.Configuration)
            .FirstOrDefaultAsync();
        var configuration = DeserializeStepConfiguration(configurationJson);
        var configuredFields = configuration?.FormFields
            ?.Where(field => !string.IsNullOrWhiteSpace(field.Name))
            .Select(field => field.Name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];
        if (configuredFields.Count > 0)
        {
            return configuredFields;
        }

        if (!string.Equals(
                procedureCase.EntityType,
                "EstatePropertyManagementListingApplication",
                StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        return procedureCase.CurrentStageIndex switch
        {
            0 => ["customerValidationStatus", "listingValidationStatus", "applicationStatus", "notes"],
            1 => ["availabilityCheck", "commercialReviewStatus", "reservationStatus", "notes"],
            2 => ["decisionStatus", "moveInDate", "notes"],
            3 => ["customerNotificationStatus", "reservationStatus", "notes"],
            _ => ["customerNotificationStatus", "applicationStatus", "notes"]
        };
    }

    private static ProcedureCaseFieldDto ToFieldDto(ProcedureCaseField field) =>
        new(field.Id, field.Key, field.Label, field.FieldType, field.Value, ParseOptions(field.OptionsJson));

    private static ProcedureCaseChecklistItemDto ToChecklistDto(ProcedureCaseChecklistItem item) =>
        new(item.Id, item.StageIndex, item.StageName, item.Text, item.IsCompleted, item.CompletedById, item.CompletedAt);

    private static ProcedureCaseDocumentDto ToDocumentDto(ProcedureCaseDocument document) =>
        new(
            document.Id,
            document.Name,
            document.RequiredFrom,
            document.ProvidedBy,
            document.IsMandatory,
            document.FileName,
            string.IsNullOrWhiteSpace(document.FileUrl)
                ? null
                : IsDocumentManagementUrl(document.FileUrl)
                    ? document.FileUrl
                : $"/api/procedure-cases/{document.ProcedureCaseId}/documents/{document.Id}/content",
            document.Notes,
            document.UploadedById,
            document.UploadedAt);

    private static bool IsDocumentManagementUrl(string fileUrl)
        => fileUrl.StartsWith("/document-management", StringComparison.OrdinalIgnoreCase)
            || Uri.TryCreate(fileUrl, UriKind.Absolute, out _);

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
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() }
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
            : await ResolveCurrentWorkflowAssignmentLabelAsync(tenantId, workflowInstanceId, currentStep, instance.Data ?? instance.DataContext);

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

        await SyncWorkflowCaseChecklistAndDocumentsAsync(
            tenantId,
            procedureCaseId,
            userId,
            instance.WorkflowDefinitionId,
            orderedSteps);

        _db.ProcedureCaseActivities.Add(Activity(tenantId, userId, procedureCaseId, "Workflow synced", currentStep?.Name ?? "Completed", notes));
        await _db.SaveChangesAsync();
    }

    private async Task SyncWorkflowCaseChecklistAndDocumentsAsync(
        Guid tenantId,
        Guid procedureCaseId,
        Guid userId,
        Guid workflowDefinitionId,
        IReadOnlyList<WorkflowStep> orderedSteps)
    {
        var now = DateTime.UtcNow;
        var workflowChecklist = orderedSteps
            .SelectMany((step, index) => BuildConfiguredWorkflowChecklist(step)
                .Select(text => new
                {
                    StageIndex = index,
                    StageName = step.Name,
                    Text = text
                }))
            .Where(item => !string.IsNullOrWhiteSpace(item.Text))
            .GroupBy(item => $"{item.StageIndex}|{item.StageName}|{item.Text}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        var checklistKeys = workflowChecklist
            .Select(item => $"{item.StageIndex}|{item.StageName}|{item.Text}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var existingChecklistItems = await _db.ProcedureCaseChecklistItems
            .IgnoreQueryFilters()
            .Where(item => item.TenantId == tenantId && item.ProcedureCaseId == procedureCaseId && !item.IsDeleted)
            .ToListAsync();

        foreach (var staleItem in existingChecklistItems.Where(item =>
                     !checklistKeys.Contains($"{item.StageIndex}|{item.StageName}|{item.Text}")))
        {
            staleItem.IsDeleted = true;
            staleItem.UpdatedAt = now;
            staleItem.LastModifiedById = userId;
        }

        foreach (var checklistItem in workflowChecklist)
        {
            if (existingChecklistItems.Any(item =>
                    item.StageIndex == checklistItem.StageIndex
                    && string.Equals(item.StageName, checklistItem.StageName, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(item.Text, checklistItem.Text, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            _db.ProcedureCaseChecklistItems.Add(new ProcedureCaseChecklistItem
            {
                TenantId = tenantId,
                ProcedureCaseId = procedureCaseId,
                StageIndex = checklistItem.StageIndex,
                StageName = checklistItem.StageName,
                Text = checklistItem.Text,
                CreatedById = userId,
                CreatedAt = now
            });
        }

        var workflowDocuments = await BuildWorkflowDocumentSeedsAsync(workflowDefinitionId);
        var documentKeys = workflowDocuments
            .Select(item => $"{item.RequiredFrom}|{item.Name}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingDocuments = await _db.ProcedureCaseDocuments
            .IgnoreQueryFilters()
            .Where(item => item.TenantId == tenantId && item.ProcedureCaseId == procedureCaseId && !item.IsDeleted)
            .ToListAsync();

        foreach (var staleDocument in existingDocuments.Where(item =>
                     !documentKeys.Contains($"{item.RequiredFrom}|{item.Name}")
                     && string.IsNullOrWhiteSpace(item.FileName)
                     && string.IsNullOrWhiteSpace(item.FileUrl)))
        {
            staleDocument.IsDeleted = true;
            staleDocument.UpdatedAt = now;
            staleDocument.LastModifiedById = userId;
        }

        foreach (var document in workflowDocuments)
        {
            if (existingDocuments.Any(item =>
                    string.Equals(item.RequiredFrom, document.RequiredFrom, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(item.Name, document.Name, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            _db.ProcedureCaseDocuments.Add(new ProcedureCaseDocument
            {
                TenantId = tenantId,
                ProcedureCaseId = procedureCaseId,
                Name = document.Name,
                RequiredFrom = document.RequiredFrom,
                ProvidedBy = ResolveDocumentProvider(document.Name, document.ProvidedBy),
                IsMandatory = document.IsMandatory,
                CreatedById = userId,
                CreatedAt = now
            });
        }
    }

    private async Task<string?> ResolveCurrentWorkflowAssignmentLabelAsync(
        Guid tenantId,
        Guid workflowInstanceId,
        WorkflowStep currentStep,
        string? contextJson)
    {
        var labels = new List<string>();
        var activeStepInstance = await _db.WorkflowStepInstances
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId
                && item.WorkflowInstanceId == workflowInstanceId
                && item.WorkflowStepId == currentStep.Id
                && (item.Status == WorkflowStepInstanceStatus.Pending || item.Status == WorkflowStepInstanceStatus.InProgress))
            .OrderByDescending(item => item.StartedDate ?? item.CreatedAt)
            .FirstOrDefaultAsync();

        if (activeStepInstance is not null)
        {
            if (activeStepInstance.AssignedToId.HasValue)
            {
                labels.Add(UserAssignmentToken(activeStepInstance.AssignedToId.Value));
            }

            var pendingApprovals = await _db.WorkflowApprovals
                .AsNoTracking()
                .Where(item => item.TenantId == tenantId
                    && item.StepInstanceId == activeStepInstance.Id
                    && item.Status == WorkflowApprovalStatus.Pending)
                .Select(item => new
                {
                    item.ApproverId,
                    item.ApproverRole
                })
                .ToListAsync();

            foreach (var approval in pendingApprovals)
            {
                if (approval.ApproverId.HasValue)
                {
                    labels.Add(UserAssignmentToken(approval.ApproverId.Value));
                }

                if (!string.IsNullOrWhiteSpace(approval.ApproverRole))
                {
                    labels.Add(approval.ApproverRole.Trim());
                }
            }
        }

        labels.AddRange(SplitAssignmentLabels(ResolveStepAssignmentLabel(currentStep, contextJson)));
        labels.Add(currentStep.Name);

        return JoinAssignmentLabels(labels);
    }

    private static string? ResolveStepAssignmentLabel(WorkflowStep step, string? contextJson)
    {
        var labels = new List<string>();

        if (!string.IsNullOrWhiteSpace(step.RequiredRole))
        {
            labels.Add(step.RequiredRole.Trim());
        }

        var config = DeserializeStepConfiguration(step.Configuration);
        labels.AddRange(ResolveAssignmentRuleLabels(config?.AssignmentRules, contextJson));
        labels.AddRange(SplitAssignmentLabels(ExtractAssignedRole(step.AssignmentConfiguration)));

        return JoinAssignmentLabels(labels);
    }

    private static IEnumerable<string> ResolveAssignmentRuleLabels(
        IEnumerable<WorkflowAssignmentRuleDto>? rules,
        string? contextJson)
    {
        if (rules is null)
        {
            yield break;
        }

        foreach (var rule in rules.OrderByDescending(item => item.Priority))
        {
            switch (rule.AssignmentType)
            {
                case WorkflowAssignmentType.User when rule.UserId.HasValue:
                    yield return UserAssignmentToken(rule.UserId.Value);
                    break;
                case WorkflowAssignmentType.Role when !string.IsNullOrWhiteSpace(rule.Role):
                    yield return rule.Role.Trim();
                    break;
                case WorkflowAssignmentType.Dynamic when TryResolveGuidFromContextJson(contextJson, rule.DynamicExpression, out var dynamicUserId):
                    yield return UserAssignmentToken(dynamicUserId);
                    break;
                case WorkflowAssignmentType.RequestorManager when TryResolveGuidFromContextJson(contextJson, "requestorManagerId", out var managerId):
                    yield return UserAssignmentToken(managerId);
                    break;
                default:
                    break;
            }
        }
    }

    private static bool TryResolveGuidFromContextJson(string? contextJson, string? expression, out Guid value)
    {
        value = Guid.Empty;
        if (string.IsNullOrWhiteSpace(contextJson) || string.IsNullOrWhiteSpace(expression))
        {
            return false;
        }

        var key = expression.Trim()
            .Trim('$')
            .Trim('.')
            .Split('.', StringSplitOptions.RemoveEmptyEntries)
            .LastOrDefault();
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(contextJson);
            if (!TryGetJsonProperty(doc.RootElement, key, out var element))
            {
                return false;
            }

            var text = element.ValueKind == JsonValueKind.String ? element.GetString() : element.ToString();
            return Guid.TryParse(text, out value);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryGetJsonProperty(JsonElement element, string key, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, key, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }

                if (TryGetJsonProperty(property.Value, key, out value))
                {
                    return true;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray())
            {
                if (TryGetJsonProperty(child, key, out value))
                {
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static IReadOnlyList<string> SplitAssignmentLabels(string? labels)
        => string.IsNullOrWhiteSpace(labels)
            ? []
            : labels.Split(['/', ',', ';', '|'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    private static string? JoinAssignmentLabels(IEnumerable<string?> labels)
    {
        var distinct = labels
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return distinct.Count == 0 ? null : string.Join(" / ", distinct);
    }

    private static string UserAssignmentToken(Guid userId) => $"User:{userId}";

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

    private sealed record DocumentSeed(
        string Name,
        string? RequiredFrom,
        bool IsMandatory,
        string ProvidedBy = "Internal",
        string? DocumentType = null,
        string AppliesTo = "All");

    private sealed record LinkedLegalMatter(
        string Purpose,
        string EntityType,
        string Title,
        string? InstrumentType,
        bool AllowRepeat,
        bool RequiresGeneratedAgreement,
        bool RequiresExecutedAgreement);

    private sealed record EstateProcedureHandoff(
        string TargetModule,
        IReadOnlyList<string> Roles,
        string Reason,
        string TriggerStage);
}
