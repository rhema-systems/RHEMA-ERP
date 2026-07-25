using ErpSystem.Core.DTOs.Estate;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.Estate;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Estate;
using ErpSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Estate;

[ApiController]
[Route("api/estate/managed-assets")]
[Authorize]
public sealed class EstateManagedAssetsController : ControllerBase
{
    private readonly IEstateManagedAssetService _managedAssetService;
    private readonly IFileStorageService _fileStorageService;
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;

    public EstateManagedAssetsController(
        IEstateManagedAssetService managedAssetService,
        IFileStorageService fileStorageService,
        ApplicationDbContext db,
        ICurrentUserService currentUserService)
    {
        _managedAssetService = managedAssetService;
        _fileStorageService = fileStorageService;
        _db = db;
        _currentUserService = currentUserService;
    }

    [HttpGet]
    public async Task<IActionResult> GetManagedAssets(
        [FromQuery] EstateManagedAssetType? assetType = null,
        [FromQuery] EstateManagedAssetStatus? status = null,
        [FromQuery] string? search = null,
        [FromQuery] bool? availableForLease = null,
        [FromQuery] bool? availableForSale = null,
        [FromQuery] int take = 100)
    {
        var assets = await _managedAssetService.GetManagedAssetsAsync(new EstateManagedAssetQuery
        {
            AssetType = assetType,
            Status = status,
            Search = search,
            AvailableForLease = availableForLease,
            AvailableForSale = availableForSale,
            Take = take
        });

        return Ok(new
        {
            success = true,
            data = assets
        });
    }

    [HttpPost("manual-land")]
    [Authorize(Roles = "admin,Admin,SystemAdmin,SuperAdmin,TenantAdmin,Estate Officer,Estate Manager,Land Registry Officer")]
    public async Task<IActionResult> CreateManualLand([FromBody] CreateManualExistingLandDto request)
    {
        if (request.IsReadyForProjectManagement && !CanMarkReadyForProjectManagement())
        {
            return Forbid();
        }

        try
        {
            var asset = await _managedAssetService.CreateManualExistingLandAsync(request);
            return Ok(new { success = true, data = asset, message = "Existing land added to the land bank." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/ready-for-project-management")]
    [Authorize(Roles = "admin,Admin,SystemAdmin,SuperAdmin,TenantAdmin,Estate Manager,Land Registry Officer")]
    public async Task<IActionResult> MarkReadyForProjectManagement(Guid id)
    {
        if (!CanMarkReadyForProjectManagement())
        {
            return Forbid();
        }

        try
        {
            var asset = await _managedAssetService.MarkReadyForProjectManagementAsync(id);
            return Ok(new { success = true, data = asset, message = "Land is ready for project management." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPatch("{id:guid}/demarcation")]
    [Authorize(Roles = "admin,Admin,SystemAdmin,SuperAdmin,TenantAdmin,Estate Officer,Estate Manager,Land Registry Officer,Survey Officer")]
    public async Task<IActionResult> UpdateLandDemarcation(Guid id, [FromBody] UpdateEstateManagedLandDemarcationDto request)
    {
        if (request.IsReadyForProjectManagement && !CanMarkReadyForProjectManagement())
        {
            return Forbid();
        }

        try
        {
            var asset = await _managedAssetService.UpdateLandDemarcationAsync(id, request);
            return Ok(new { success = true, data = asset, message = "Land demarcation updated." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPatch("{id:guid}/external-listing")]
    [Authorize(Roles = "admin,Admin,SystemAdmin,SuperAdmin,TenantAdmin,Estate Officer,Estate Manager,Property Manager")]
    public async Task<IActionResult> UpdateExternalListing(Guid id, [FromBody] UpdateEstateManagedAssetListingDto request)
    {
        try
        {
            var asset = await _managedAssetService.UpdateExternalListingAsync(id, request);
            return Ok(new { success = true, data = asset, message = "External portal listing updated." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpGet("{id:guid}/documents")]
    public async Task<IActionResult> GetDocuments(Guid id)
        => Ok(new { success = true, data = await _managedAssetService.GetDocumentsAsync(id) });

    [HttpPost("{id:guid}/documents")]
    [Authorize(Roles = "admin,Admin,SystemAdmin,SuperAdmin,TenantAdmin,Estate Officer,Estate Manager,Land Registry Officer,Survey Officer")]
    [RequestSizeLimit(52_428_800)]
    public async Task<IActionResult> UploadDocument(
        Guid id,
        [FromForm] IFormFile file,
        [FromForm] string documentType,
        [FromForm] string? documentName,
        [FromForm] bool isListingImage = false,
        [FromForm] bool isPrimaryListingImage = false)
    {
        if (file == null || file.Length == 0) return BadRequest(new { success = false, message = "Select a document to upload." });
        if ((isListingImage || isPrimaryListingImage) && !IsImage(file))
        {
            return BadRequest(new { success = false, message = "Listing images must be image files." });
        }

        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        var assetExists = tenantId != Guid.Empty
            && await _db.EstateManagedAssets.AnyAsync(
                item => item.Id == id && item.TenantId == tenantId && !item.IsDeleted);
        if (!assetExists)
        {
            return NotFound(new { success = false, message = "Estate asset was not found." });
        }

        var folder = $"estate/managed-assets/{id:N}";
        await using var stream = file.OpenReadStream();
        var filePath = await _fileStorageService.UploadFileAsync(stream, file.FileName, folder);
        var document = await _managedAssetService.RegisterDocumentAsync(id, new RegisterEstateManagedAssetDocumentDto
        {
            FileName = file.FileName,
            FilePath = filePath,
            DocumentType = documentType,
            DocumentName = documentName,
            ContentType = file.ContentType,
            FileSize = file.Length,
            IsListingImage = isListingImage || isPrimaryListingImage,
            IsPrimaryListingImage = isPrimaryListingImage
        });
        return Ok(new { success = true, data = document, message = "Estate asset file uploaded to the file repository." });
    }

    [HttpPost("{id:guid}/documents/{documentId:guid}/primary-listing-image")]
    [Authorize(Roles = "admin,Admin,SystemAdmin,SuperAdmin,TenantAdmin,Estate Officer,Estate Manager,Property Manager")]
    public async Task<IActionResult> SetPrimaryListingImage(Guid id, Guid documentId)
    {
        try
        {
            var document = await _managedAssetService.SetPrimaryListingImageAsync(id, documentId);
            return Ok(new { success = true, data = document, message = "Primary listing image updated." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpGet("{id:guid}/documents/{documentId:guid}/download")]
    public async Task<IActionResult> DownloadDocument(Guid id, Guid documentId)
    {
        try
        {
            var stored = await _managedAssetService.GetDocumentAsync(id, documentId);
            var stream = await _fileStorageService.DownloadFileAsync(stored.FilePath, documentId);
            return File(stream, stored.Document.ContentType ?? "application/octet-stream", stored.Document.FileName);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/documents/{documentId:guid}/publish-to-dms")]
    [Authorize(Roles = "admin,Admin,SystemAdmin,SuperAdmin,TenantAdmin,Estate Officer,Estate Manager,Land Registry Officer,Survey Officer")]
    public async Task<IActionResult> PublishDocumentToCentralDms(Guid id, Guid documentId, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var asset = await _db.EstateManagedAssets
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id && item.TenantId == tenantId && !item.IsDeleted, cancellationToken);

        if (asset == null)
        {
            return NotFound(new { success = false, message = "Estate asset was not found." });
        }

        var document = await _db.EstateManagedAssetDocuments
            .FirstOrDefaultAsync(item => item.Id == documentId
                && item.EstateManagedAssetId == id
                && item.TenantId == tenantId
                && !item.IsDeleted, cancellationToken);

        if (document == null)
        {
            return NotFound(new { success = false, message = "Estate document was not found." });
        }

        var existingRecord = document.CentralDocumentRecordId.HasValue
            ? await _db.CentralDocumentRecords
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == document.CentralDocumentRecordId.Value
                    && item.TenantId == tenantId
                    && !item.IsDeleted, cancellationToken)
            : await _db.CentralDocumentRecords
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.TenantId == tenantId
                    && (item.SourceModule == "Estate / Facilities"
                        || item.SourceModule == "Estate / Facility")
                    && item.SourceEntityType == "EstateManagedAssetDocument"
                    && item.SourceRecordId == document.Id
                    && !item.IsDeleted, cancellationToken);

        if (existingRecord != null)
        {
            document.CentralDocumentRecordId = existingRecord.Id;
            document.CentralDocumentReference = existingRecord.DocumentReference;
            document.PublishedToCentralDmsAt ??= existingRecord.PublishedAt ?? DateTime.UtcNow;
            document.UpdatedAt = DateTime.UtcNow;
            document.UpdatedBy = _currentUserService.UserName ?? "System";
            document.LastModifiedById = GetUserId();
            await _db.SaveChangesAsync(cancellationToken);

            return Ok(new
            {
                success = true,
                data = ToCentralDmsPublicationDto(existingRecord, document.PublishedToCentralDmsAt),
                message = "Estate document is already published to Central DMS."
            });
        }

        var template = await ResolveMetadataTemplateAsync(tenantId, document.DocumentType, cancellationToken);
        var now = DateTime.UtcNow;
        var documentReference = await NextDocumentReferenceAsync(tenantId, cancellationToken);
        var sourceRecordReference = $"{asset.AssetCode} / {document.DocumentType} / {document.FileName}";
        var title = string.IsNullOrWhiteSpace(document.DocumentName) ? document.FileName : document.DocumentName.Trim();
        var repositoryStatus = string.IsNullOrWhiteSpace(document.FilePath) ? "Not linked" : "Linked";
        var annotationStatus = IsPdf(document) ? "PDF preview ready" : "Not required";

        var record = new CentralDocumentRecord
        {
            TenantId = tenantId,
            DocumentReference = documentReference,
            Title = title,
            SourceModule = "Estate / Facilities",
            SourceLabel = "Source: Estate / Facilities -> Central DMS",
            SourceEntityType = "EstateManagedAssetDocument",
            SourceRecordReference = Truncate(sourceRecordReference, 180),
            SourceRecordId = document.Id,
            MetadataTemplateCode = template?.TemplateCode ?? BuildTemplateCode(document.DocumentType),
            RepositoryStatus = repositoryStatus,
            RepositoryPath = document.FilePath,
            CurrentVersion = "v1.0",
            VersionStatus = "Published",
            AnnotationStatus = annotationStatus,
            CommentStatus = "Open for comments",
            AccessProfile = string.IsNullOrWhiteSpace(template?.AccessProfile) ? "Module restricted" : template.AccessProfile,
            RetentionStatus = "Current",
            LifecycleStatus = "Active",
            PublishedAt = now,
            PublishedById = GetUserId(),
            Notes = $"Published from Estate asset {asset.AssetCode} ({asset.Name}).",
            CreatedAt = now,
            CreatedBy = _currentUserService.UserName ?? "System",
            CreatedById = GetUserId()
        };

        var version = new CentralDocumentVersion
        {
            TenantId = tenantId,
            DocumentRecordId = record.Id,
            VersionNumber = "v1.0",
            Status = "Published",
            RepositoryPath = document.FilePath,
            FileName = document.FileName,
            ContentType = document.ContentType,
            FileSize = document.FileSize,
            FileUploadRecordId = document.Id,
            CreatedByUserId = GetUserId(),
            PublishedAt = now,
            PublishedById = GetUserId(),
            ChangeSummary = "Initial Estate / Facilities document publication into Central DMS.",
            CreatedAt = now,
            CreatedBy = _currentUserService.UserName ?? "System",
            CreatedById = GetUserId()
        };

        _db.CentralDocumentRecords.Add(record);
        _db.CentralDocumentVersions.Add(version);

        document.CentralDocumentRecordId = record.Id;
        document.CentralDocumentReference = record.DocumentReference;
        document.PublishedToCentralDmsAt = now;
        document.UpdatedAt = now;
        document.UpdatedBy = _currentUserService.UserName ?? "System";
        document.LastModifiedById = GetUserId();

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            success = true,
            data = ToCentralDmsPublicationDto(record, document.PublishedToCentralDmsAt),
            message = "Estate document published to Central DMS."
        });
    }

    private Guid GetTenantId()
        => _currentUserService.TenantId ?? Guid.Parse("00000000-0000-0000-0000-000000000001");

    private Guid? GetUserId()
        => Guid.TryParse(_currentUserService.UserId, out var userId) ? userId : null;

    private bool CanMarkReadyForProjectManagement()
        => _currentUserService.IsInRole("admin")
            || _currentUserService.IsInRole("Admin")
            || _currentUserService.IsInRole("SystemAdmin")
            || _currentUserService.IsInRole("SuperAdmin")
            || _currentUserService.IsInRole("TenantAdmin")
            || _currentUserService.IsInRole("Estate Manager")
            || _currentUserService.IsInRole("Land Registry Officer");

    private async Task<string> NextDocumentReferenceAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        return $"DMS-{DateTime.UtcNow:yyyy}-{Guid.NewGuid():N}"[..21].ToUpperInvariant();
    }

    private async Task<CentralDocumentMetadataTemplate?> ResolveMetadataTemplateAsync(
        Guid tenantId,
        string documentType,
        CancellationToken cancellationToken)
    {
        var normalizedType = string.IsNullOrWhiteSpace(documentType) ? "Other" : documentType.Trim();
        return await _db.CentralDocumentMetadataTemplates
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId
                && item.IsActive
                && !item.IsDeleted
                && (item.Module == "Estate / Facilities"
                    || item.Module == "Estate / Facility"
                    || item.Module == "Estate"
                    || item.Module == "Facilities"))
            .OrderByDescending(item => item.DocumentType == normalizedType)
            .ThenBy(item => item.TemplateCode)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static bool IsPdf(EstateManagedAssetDocument document)
        => string.Equals(document.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase)
            || document.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);

    private static bool IsImage(IFormFile file)
        => file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
            || file.FileName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
            || file.FileName.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
            || file.FileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
            || file.FileName.EndsWith(".webp", StringComparison.OrdinalIgnoreCase);

    private static string BuildTemplateCode(string documentType)
    {
        var normalized = new string((string.IsNullOrWhiteSpace(documentType) ? "OTHER" : documentType)
            .Trim()
            .ToUpperInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray());

        while (normalized.Contains("--", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("--", "-", StringComparison.Ordinal);
        }

        normalized = normalized.Trim('-');
        return Truncate($"EST-FAC-{(string.IsNullOrWhiteSpace(normalized) ? "OTHER" : normalized)}", 80);
    }

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];

    private static object ToCentralDmsPublicationDto(CentralDocumentRecord record, DateTime? publishedAt) => new
    {
        record.Id,
        record.DocumentReference,
        record.Title,
        record.SourceModule,
        record.SourceLabel,
        record.SourceEntityType,
        record.SourceRecordReference,
        record.SourceRecordId,
        record.MetadataTemplateCode,
        record.RepositoryStatus,
        record.RepositoryPath,
        record.CurrentVersion,
        record.VersionStatus,
        record.AnnotationStatus,
        record.CommentStatus,
        record.AccessProfile,
        record.RetentionStatus,
        record.LifecycleStatus,
        PublishedToCentralDmsAt = publishedAt ?? record.PublishedAt
    };
}
