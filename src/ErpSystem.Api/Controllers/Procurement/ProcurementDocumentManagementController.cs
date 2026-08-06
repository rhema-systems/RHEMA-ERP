using System.Text.Json;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.DocumentManagement;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Procurement;

[ApiController]
[Route("api/procurement/document-management")]
[Authorize(Policy = "InternalOnly")]
public sealed class ProcurementDocumentManagementController : ControllerBase
{
    private static readonly HashSet<string> ProcurementWorkflowEntityTypes =
        ProcurementAccessControlRegistry.Workflows
            .Select(item => item.EntityTypeCode)
            .Append("VendorInvoiceMatchException")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserProvider _currentUser;
    private readonly IAuthorizationService _authorization;
    private readonly IControlledFileUploadService _controlledFiles;
    private readonly ICentralDocumentRepositoryFileService _centralDocuments;

    public ProcurementDocumentManagementController(
        ApplicationDbContext db,
        ICurrentUserProvider currentUser,
        IAuthorizationService authorization,
        IControlledFileUploadService controlledFiles,
        ICentralDocumentRepositoryFileService centralDocuments)
    {
        _db = db;
        _currentUser = currentUser;
        _authorization = authorization;
        _controlledFiles = controlledFiles;
        _centralDocuments = centralDocuments;
    }

    [HttpGet("catalogue")]
    public async Task<ActionResult<IReadOnlyList<ProcurementDocumentFamilyDto>>> GetCatalogue(
        CancellationToken cancellationToken)
    {
        var result = new List<ProcurementDocumentFamilyDto>();
        foreach (var definition in ProcurementDocumentManagementCatalog.Families)
        {
            var canRead = await HasAnyPermissionAsync([definition.ReadPermission], cancellationToken);
            if (!canRead)
            {
                continue;
            }

            result.Add(new ProcurementDocumentFamilyDto
            {
                Code = definition.Code,
                Name = definition.Name,
                Description = definition.Description,
                TemplateCode = definition.TemplateCode,
                AccessProfile = definition.AccessProfile,
                Classifications = definition.Classifications,
                CanRead = true,
                CanUpload = await HasAnyPermissionAsync(definition.UploadPermissions, cancellationToken)
            });
        }

        return Ok(result);
    }

    [HttpGet("sources")]
    public async Task<ActionResult<IReadOnlyList<ProcurementDocumentSourceOptionDto>>> GetSources(
        [FromQuery] ProcurementDocumentFamily family,
        CancellationToken cancellationToken)
    {
        var definition = ProcurementDocumentManagementCatalog.Find(family);
        if (definition is null)
        {
            return ValidationProblem("Select a supported procurement document family.");
        }
        if (!await HasAnyPermissionAsync([definition.ReadPermission], cancellationToken))
        {
            return Forbid();
        }

        return Ok(await LoadSourcesAsync(family, null, cancellationToken));
    }

    [HttpGet("records")]
    public async Task<ActionResult<IReadOnlyList<ProcurementManagedDocumentDto>>> GetRecords(
        [FromQuery] ProcurementDocumentFamily family,
        [FromQuery] Guid? sourceRecordId,
        CancellationToken cancellationToken)
    {
        var definition = ProcurementDocumentManagementCatalog.Find(family);
        if (definition is null)
        {
            return ValidationProblem("Select a supported procurement document family.");
        }
        if (!await HasAnyPermissionAsync([definition.ReadPermission], cancellationToken))
        {
            return Forbid();
        }

        return Ok(await LoadRecordsAsync(definition, sourceRecordId, null, cancellationToken));
    }

    [HttpPost("records")]
    [RequestSizeLimit(50 * 1024 * 1024)]
    public async Task<ActionResult<ProcurementManagedDocumentDto>> Upload(
        [FromForm] ProcurementDocumentFamily family,
        [FromForm] Guid sourceRecordId,
        [FromForm] string classification,
        [FromForm] IFormFile file,
        [FromForm] string? title,
        CancellationToken cancellationToken)
    {
        var definition = ProcurementDocumentManagementCatalog.Find(family);
        if (definition is null)
        {
            return ValidationProblem("Select a supported procurement document family.");
        }
        if (!await HasAnyPermissionAsync(definition.UploadPermissions, cancellationToken))
        {
            return Forbid();
        }

        if (sourceRecordId == Guid.Empty)
        {
            return ValidationProblem("A source record selected from the controlled list is required.");
        }

        var selectedClassification = classification?.Trim();
        if (string.IsNullOrWhiteSpace(selectedClassification) ||
            !definition.Classifications.Contains(selectedClassification, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationProblem("Select a supported document classification from the controlled list.");
        }

        if (file is null || file.Length == 0)
        {
            return ValidationProblem("A document file is required.");
        }

        var source = (await LoadSourcesAsync(family, sourceRecordId, cancellationToken)).SingleOrDefault();
        if (source is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Procurement source not found",
                Status = StatusCodes.Status404NotFound,
                Detail = "The selected source record is unavailable in this tenant.",
                Extensions = { ["code"] = "PROCUREMENT_DMS_SOURCE_NOT_FOUND" }
            });
        }

        var safeFileName = Path.GetFileName(file.FileName);
        var documentTitle = string.IsNullOrWhiteSpace(title) ? safeFileName : title.Trim();
        if (documentTitle.Length > 250)
        {
            return ValidationProblem("Document title cannot exceed 250 characters.");
        }

        ControlledFileUploadResult upload;
        try
        {
            upload = await _controlledFiles.UploadAsync(
                new ControlledFileUploadRequest
                {
                    TenantId = _currentUser.TenantId,
                    ActorUserId = _currentUser.UserId,
                    ActorName = _currentUser.FullName,
                    Category = ControlledFileUploadCategories.DocumentManagement,
                    FileName = safeFileName,
                    ContentType = string.IsNullOrWhiteSpace(file.ContentType)
                        ? "application/octet-stream"
                        : file.ContentType,
                    FileSize = file.Length,
                    OpenReadStream = file.OpenReadStream
                },
                cancellationToken);
        }
        catch (ControlledFileUploadException exception)
        {
            return StatusCode(exception.StatusCode, new ProblemDetails
            {
                Title = "Procurement document upload failed",
                Status = exception.StatusCode,
                Detail = exception.Message,
                Extensions = { ["code"] = exception.Code }
            });
        }

        var duplicate = await (from record in _db.CentralDocumentRecords.AsNoTracking()
                               join value in _db.CentralDocumentMetadataValues.AsNoTracking()
                                   on record.Id equals value.DocumentRecordId
                               where record.TenantId == _currentUser.TenantId &&
                                     !record.IsDeleted &&
                                     record.MetadataTemplateCode == definition.TemplateCode &&
                                     record.SourceRecordId == sourceRecordId &&
                                     value.TenantId == _currentUser.TenantId &&
                                     !value.IsDeleted &&
                                     value.FieldKey == "checksumsha256" &&
                                     value.FieldValue == upload.ChecksumSha256
                               select record.Id)
            .AnyAsync(cancellationToken);
        if (duplicate)
        {
            await _controlledFiles.DeleteAsync(
                _currentUser.TenantId, upload.Record.Id, _currentUser.UserId, cancellationToken);
            return Conflict(new ProblemDetails
            {
                Title = "Duplicate procurement document",
                Status = StatusCodes.Status409Conflict,
                Detail = "The same file is already registered for this procurement source.",
                Extensions = { ["code"] = "PROCUREMENT_DMS_DUPLICATE" }
            });
        }

        CentralDocumentRepositoryLink link;
        try
        {
            link = await _centralDocuments.RegisterAsync(
                new CentralDocumentRepositoryRegistration
                {
                    TenantId = _currentUser.TenantId,
                    ActorUserId = _currentUser.UserId,
                    ActorName = _currentUser.FullName,
                    FileUploadRecordId = upload.Record.Id,
                    SourceModule = ProcurementDocumentManagementCatalog.SourceModule,
                    SourceLabel = $"Procurement / {definition.Name}",
                    SourceEntityType = definition.SourceEntityType,
                    SourceRecordId = source.Id,
                    SourceRecordReference = source.Reference,
                    Title = documentTitle,
                    DocumentType = definition.TemplateDocumentType,
                    MetadataTemplateCode = definition.TemplateCode,
                    AccessProfile = definition.AccessProfile,
                    VersionNumber = "v1.0",
                    VersionStatus = "Submitted",
                    ChangeSummary = $"{selectedClassification} uploaded from the procurement source record.",
                    RequirePublishedGovernance = true,
                    MetadataValues = BuildMetadata(
                        family, source, selectedClassification, upload.ChecksumSha256)
                },
                cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            await _controlledFiles.DeleteAsync(
                _currentUser.TenantId, upload.Record.Id, _currentUser.UserId, cancellationToken);
            return UnprocessableEntity(new ProblemDetails
            {
                Title = "Procurement document governance failed",
                Status = StatusCodes.Status422UnprocessableEntity,
                Detail = exception.Message,
                Extensions = { ["code"] = "PROCUREMENT_DMS_GOVERNANCE_FAILED" }
            });
        }
        catch
        {
            await _controlledFiles.DeleteAsync(
                _currentUser.TenantId, upload.Record.Id, _currentUser.UserId, cancellationToken);
            throw;
        }

        var created = (await LoadRecordsAsync(
            definition, sourceRecordId, link.DocumentRecordId, cancellationToken)).Single();
        return Created(
            $"/api/procurement/document-management/records/{created.DocumentRecordId}",
            created);
    }

    [HttpGet("records/{documentRecordId:guid}/versions/{documentVersionId:guid}/download")]
    public async Task<IActionResult> Download(
        Guid documentRecordId,
        Guid documentVersionId,
        CancellationToken cancellationToken)
    {
        var record = await _db.CentralDocumentRecords
            .AsNoTracking()
            .SingleOrDefaultAsync(item =>
                    item.Id == documentRecordId &&
                    item.TenantId == _currentUser.TenantId &&
                    item.SourceModule == ProcurementDocumentManagementCatalog.SourceModule &&
                    !item.IsDeleted,
                cancellationToken);
        if (record is null)
        {
            return NotFound();
        }

        var definition = ProcurementDocumentManagementCatalog.Families
            .SingleOrDefault(item => item.TemplateCode == record.MetadataTemplateCode);
        if (definition is null ||
            !await HasAnyPermissionAsync([definition.ReadPermission], cancellationToken))
        {
            return Forbid();
        }

        var content = await _centralDocuments.OpenAsync(
            _currentUser.TenantId, documentRecordId, documentVersionId, cancellationToken);
        if (content is null)
        {
            return NotFound();
        }

        if (content.UploadRecord.VirusScanStatus != FileVirusScanStatus.Clean)
        {
            await content.DisposeAsync();
            return Conflict(new ProblemDetails
            {
                Title = "Procurement document unavailable",
                Status = StatusCodes.Status409Conflict,
                Detail = "The current centralized malware result does not permit retrieval.",
                Extensions = { ["code"] = "PROCUREMENT_DMS_MALWARE_NOT_CLEAN" }
            });
        }

        _db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = _currentUser.TenantId,
            UserId = _currentUser.UserId,
            Username = _currentUser.Username,
            Action = "CentralDmsDocumentDownloaded",
            Resource = definition.SourceEntityType,
            ResourceId = record.SourceRecordId?.ToString(),
            NewValues = JsonSerializer.Serialize(new
            {
                documentRecordId,
                documentVersionId,
                record.DocumentReference,
                definition.Code
            }),
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            UserAgent = Request.Headers.UserAgent.ToString(),
            Timestamp = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);

        return File(content.Content, content.ContentType, content.FileName, enableRangeProcessing: true);
    }

    private async Task<IReadOnlyList<ProcurementDocumentSourceOptionDto>> LoadSourcesAsync(
        ProcurementDocumentFamily family,
        Guid? exactId,
        CancellationToken cancellationToken)
    {
        var tenantId = _currentUser.TenantId;
        const int take = 250;

        switch (family)
        {
            case ProcurementDocumentFamily.Tender:
                return await _db.Tenders.AsNoTracking()
                    .Where(item => item.TenantId == tenantId && !item.IsDeleted &&
                                   (!exactId.HasValue || item.Id == exactId.Value))
                    .OrderByDescending(item => item.CreatedAt).Take(take)
                    .Select(item => new ProcurementDocumentSourceOptionDto
                    {
                        Id = item.Id,
                        Reference = item.TenderNumber,
                        Label = item.TenderNumber + " · " + item.Title,
                        Status = item.Status
                    })
                    .ToListAsync(cancellationToken);

            case ProcurementDocumentFamily.Evaluation:
                return await _db.TenderEvaluations.AsNoTracking()
                    .Where(item => item.TenantId == tenantId && !item.IsDeleted &&
                                   (!exactId.HasValue || item.Id == exactId.Value))
                    .OrderByDescending(item => item.EvaluationDate).Take(take)
                    .Select(item => new ProcurementDocumentSourceOptionDto
                    {
                        Id = item.Id,
                        Reference = item.TenderBid.BidNumber,
                        Label = item.TenderBid.Tender.TenderNumber + " · " + item.TenderBid.BidNumber,
                        Status = item.Status
                    })
                    .ToListAsync(cancellationToken);

            case ProcurementDocumentFamily.Approval:
                var workflowCodes = ProcurementWorkflowEntityTypes.ToList();
                var approvals = await _db.WorkflowInstances.AsNoTracking()
                    .Where(item => item.TenantId == tenantId && !item.IsDeleted &&
                                   workflowCodes.Contains(item.EntityType.Code) &&
                                   (!exactId.HasValue || item.Id == exactId.Value))
                    .OrderByDescending(item => item.CreatedDate).Take(take)
                    .Select(item => new
                    {
                        item.Id,
                        item.EntityId,
                        EntityCode = item.EntityType.Code,
                        EntityName = item.EntityType.Name,
                        item.Status
                    })
                    .ToListAsync(cancellationToken);
                return approvals.Select(item => Option(
                    item.Id,
                    $"{item.EntityCode}:{item.EntityId:N}",
                    $"{item.EntityName} · {item.EntityId:N}",
                    item.Status.ToString())).ToList();

            case ProcurementDocumentFamily.Supplier:
                return await _db.BusinessPartnerRegistrations.AsNoTracking()
                    .Where(item => item.TenantId == tenantId && !item.IsDeleted &&
                                   (!exactId.HasValue || item.Id == exactId.Value))
                    .OrderByDescending(item => item.CreatedAt).Take(take)
                    .Select(item => new ProcurementDocumentSourceOptionDto
                    {
                        Id = item.Id,
                        Reference = item.RegistrationNumber,
                        Label = item.RegistrationNumber + " · " + item.ApplicantName,
                        Status = item.Status
                    })
                    .ToListAsync(cancellationToken);

            case ProcurementDocumentFamily.Contract:
                return await _db.Contracts.AsNoTracking()
                    .Where(item => item.TenantId == tenantId && !item.IsDeleted &&
                                   (!exactId.HasValue || item.Id == exactId.Value))
                    .OrderByDescending(item => item.CreatedAt).Take(take)
                    .Select(item => new ProcurementDocumentSourceOptionDto
                    {
                        Id = item.Id,
                        Reference = item.ContractNumber,
                        Label = item.ContractNumber + " · " + item.ContractTitle,
                        Status = item.Status
                    })
                    .ToListAsync(cancellationToken);

            case ProcurementDocumentFamily.GoodsReceiptNote:
                var grns = await _db.GoodsReceiptNotes.AsNoTracking()
                    .Where(item => item.TenantId == tenantId && !item.IsDeleted &&
                                   (!exactId.HasValue || item.Id == exactId.Value))
                    .OrderByDescending(item => item.ReceiptDate).Take(take)
                    .Select(item => new
                    {
                        item.Id,
                        item.GRNNumber,
                        item.PurchaseOrderNumber,
                        item.SupplierName,
                        item.Status
                    }).ToListAsync(cancellationToken);
                return grns.Select(item => Option(item.Id, item.GRNNumber,
                    $"{item.GRNNumber} · {item.PurchaseOrderNumber ?? item.SupplierName ?? "Receipt"}",
                    item.Status.ToString())).ToList();

            case ProcurementDocumentFamily.MaterialReceiptNote:
                var mrns = await _db.ProcurementReceiptDocuments.AsNoTracking()
                    .Where(item => item.TenantId == tenantId && !item.IsDeleted &&
                                   item.DocumentKind == ProcurementReceiptDocumentKind.Mrn &&
                                   (!exactId.HasValue || item.Id == exactId.Value))
                    .OrderByDescending(item => item.PreparedAtUtc).Take(take)
                    .Select(item => new { item.Id, item.DocumentNumber, item.Status })
                    .ToListAsync(cancellationToken);
                return mrns.Select(item => Option(item.Id, item.DocumentNumber,
                    item.DocumentNumber, item.Status.ToString())).ToList();

            case ProcurementDocumentFamily.VendorInvoice:
                var invoices = await _db.VendorInvoices.AsNoTracking()
                    .Where(item => item.TenantId == tenantId && !item.IsDeleted &&
                                   (!exactId.HasValue || item.Id == exactId.Value))
                    .OrderByDescending(item => item.InvoiceDate).Take(take)
                    .Select(item => new
                    {
                        item.Id,
                        item.InvoiceNumber,
                        item.SupplierInvoiceNumber,
                        item.SupplierName,
                        item.Status
                    }).ToListAsync(cancellationToken);
                return invoices.Select(item => Option(item.Id, item.InvoiceNumber,
                    $"{item.InvoiceNumber} · {item.SupplierInvoiceNumber ?? item.SupplierName}",
                    item.Status.ToString())).ToList();

            case ProcurementDocumentFamily.Disposal:
                var disposals = await _db.InventoryDisposalCases.AsNoTracking()
                    .Where(item => item.TenantId == tenantId && !item.IsDeleted &&
                                   (!exactId.HasValue || item.Id == exactId.Value))
                    .OrderByDescending(item => item.RequestedAtUtc).Take(take)
                    .Select(item => new { item.Id, item.DisposalNumber, item.Reason, item.Status })
                    .ToListAsync(cancellationToken);
                return disposals.Select(item => Option(item.Id, item.DisposalNumber,
                    $"{item.DisposalNumber} · {item.Reason}", item.Status.ToString())).ToList();

            default:
                return [];
        }
    }

    private async Task<IReadOnlyList<ProcurementManagedDocumentDto>> LoadRecordsAsync(
        ProcurementDocumentFamilyDefinition definition,
        Guid? sourceRecordId,
        Guid? exactRecordId,
        CancellationToken cancellationToken)
    {
        var records = await _db.CentralDocumentRecords
            .AsNoTracking()
            .Include(item => item.Versions.Where(version => !version.IsDeleted))
            .Include(item => item.MetadataValues.Where(value => !value.IsDeleted))
            .Where(item => item.TenantId == _currentUser.TenantId &&
                           item.SourceModule == ProcurementDocumentManagementCatalog.SourceModule &&
                           item.MetadataTemplateCode == definition.TemplateCode &&
                           !item.IsDeleted &&
                           (!sourceRecordId.HasValue || item.SourceRecordId == sourceRecordId.Value) &&
                           (!exactRecordId.HasValue || item.Id == exactRecordId.Value))
            .OrderByDescending(item => item.CreatedAt)
            .Take(250)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        if (records.Count == 0)
        {
            return [];
        }

        var template = await _db.CentralDocumentMetadataTemplates.AsNoTracking()
            .SingleOrDefaultAsync(item => item.TenantId == _currentUser.TenantId &&
                                          item.TemplateCode == definition.TemplateCode &&
                                          item.IsActive && !item.IsDeleted,
                cancellationToken);
        var requiredFields = ParseRequiredFields(template?.RequiredFieldsJson);
        var accessCovered = await _db.CentralDocumentAccessRules.AsNoTracking()
            .AnyAsync(item => item.TenantId == _currentUser.TenantId &&
                              item.AccessProfile == definition.AccessProfile &&
                              item.IsActive && item.CanView && !item.IsDeleted &&
                              (item.Module == null || item.Module == ProcurementDocumentManagementCatalog.SourceModule),
                cancellationToken);
        var retentionCovered = await _db.CentralDocumentRetentionPolicies.AsNoTracking()
            .AnyAsync(item => item.TenantId == _currentUser.TenantId &&
                              item.IsActive && !item.IsDeleted &&
                              (item.Module == null || item.Module == ProcurementDocumentManagementCatalog.SourceModule) &&
                              (item.DocumentType == null || item.DocumentType == definition.TemplateDocumentType),
                cancellationToken);
        var uploadIds = records.SelectMany(item => item.Versions)
            .Where(item => item.FileUploadRecordId.HasValue)
            .Select(item => item.FileUploadRecordId!.Value)
            .Distinct().ToList();
        var malwareByUpload = uploadIds.Count == 0
            ? new Dictionary<Guid, FileVirusScanStatus>()
            : await _db.FileUploadRecords.AsNoTracking()
                .Where(item => item.TenantId == _currentUser.TenantId && uploadIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, item => item.VirusScanStatus, cancellationToken);

        return records.Where(record => record.Versions.Count > 0).Select(record =>
        {
            var version = record.Versions
                .OrderByDescending(item => item.CreatedAt)
                .First();
            var metadataKeys = record.MetadataValues
                .Where(item => !string.IsNullOrWhiteSpace(item.FieldValue))
                .Select(item => item.FieldKey)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var classification = record.MetadataValues
                .FirstOrDefault(item => item.FieldKey == "classification")?.FieldValue ??
                definition.TemplateDocumentType;
            var malware = version.FileUploadRecordId.HasValue &&
                          malwareByUpload.TryGetValue(version.FileUploadRecordId.Value, out var status)
                ? status.ToString()
                : "Unavailable";
            return new ProcurementManagedDocumentDto
            {
                DocumentRecordId = record.Id,
                DocumentVersionId = version.Id,
                Family = definition.Code,
                SourceRecordId = record.SourceRecordId ?? Guid.Empty,
                SourceReference = record.SourceRecordReference ?? string.Empty,
                DocumentReference = record.DocumentReference,
                Title = record.Title,
                Classification = classification,
                TemplateCode = definition.TemplateCode,
                VersionNumber = version.VersionNumber,
                VersionStatus = version.Status,
                AccessProfile = record.AccessProfile,
                RetentionStatus = record.RetentionStatus,
                LifecycleStatus = record.LifecycleStatus,
                MalwareStatus = malware,
                MetadataComplete = requiredFields.All(field => metadataKeys.Contains(field)),
                AccessCovered = accessCovered,
                RetentionCovered = retentionCovered,
                LegalHold = string.Equals(record.RetentionStatus, "Legal hold", StringComparison.OrdinalIgnoreCase),
                CreatedAtUtc = record.CreatedAt,
                CreatedBy = record.CreatedBy ?? string.Empty
            };
        }).ToList();
    }

    private async Task<bool> HasAnyPermissionAsync(
        IReadOnlyList<string> permissions,
        CancellationToken cancellationToken)
    {
        foreach (var permission in permissions)
        {
            var result = await _authorization.AuthorizeAsync(User, null, permission);
            if (result.Succeeded)
            {
                return true;
            }

            cancellationToken.ThrowIfCancellationRequested();
        }

        return false;
    }

    private static ProcurementDocumentSourceOptionDto Option(
        Guid id, string reference, string label, string status) => new()
    {
        Id = id,
        Reference = reference,
        Label = label,
        Status = status
    };

    private IReadOnlyList<CentralDocumentMetadataRegistrationValue> BuildMetadata(
        ProcurementDocumentFamily family,
        ProcurementDocumentSourceOptionDto source,
        string classification,
        string checksum) =>
    [
        new("sourceReference", "Source reference", source.Reference),
        new("documentFamily", "Document family", family.ToString()),
        new("classification", "Classification", classification),
        new("sourceStatus", "Source status", source.Status),
        new("uploadedBy", "Uploaded by", _currentUser.FullName),
        new("checksumSha256", "Checksum SHA-256", checksum)
    ];

    private static IReadOnlyList<string> ParseRequiredFields(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return (JsonSerializer.Deserialize<string[]>(json) ?? [])
                .Select(NormalizeMetadataField)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (JsonException)
        {
            return ["__invalid_template__"];
        }
    }

    private static string NormalizeMetadataField(string value) =>
        new(value.Trim().ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

    private BadRequestObjectResult ValidationProblem(string detail) =>
        BadRequest(new ProblemDetails
        {
            Title = "Procurement document validation failed",
            Status = StatusCodes.Status400BadRequest,
            Detail = detail,
            Extensions = { ["code"] = "PROCUREMENT_DMS_VALIDATION_FAILED" }
        });
}
