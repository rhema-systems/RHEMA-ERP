using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.Estate;
using ErpSystem.Core.Enums;
using ErpSystem.Core.DTOs.Procedures;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procedures;
using ErpSystem.Api.Services.Notifications;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Estate;

[ApiController]
[Route("api/estate/external/documents")]
[Authorize(Roles = Constants.Roles.ExternalUser)]
public sealed class EstateExternalDocumentsController : ControllerBase
{
    private const string PortalRecipientFieldKey = "dispatchedto";

    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;
    private readonly IProcedureCaseService _procedureCaseService;
    private readonly IFileStorageService _fileStorageService;
    private readonly INotificationService _notificationService;

    public EstateExternalDocumentsController(
        ApplicationDbContext db,
        ICurrentUserService currentUserService,
        IProcedureCaseService procedureCaseService,
        IFileStorageService fileStorageService,
        INotificationService notificationService)
    {
        _db = db;
        _currentUserService = currentUserService;
        _procedureCaseService = procedureCaseService;
        _fileStorageService = fileStorageService;
        _notificationService = notificationService;
    }

    [HttpGet]
    public async Task<IActionResult> GetMyEstateDocuments(CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        var identities = BuildIdentityTerms();
        if (tenantId == Guid.Empty || identities.Count == 0)
        {
            return Ok(new { success = true, data = Array.Empty<object>() });
        }

        var candidateRecordIds = await _db.CentralDocumentMetadataValues
            .AsNoTracking()
            .Where(value => value.TenantId == tenantId
                && !value.IsDeleted
                && value.FieldValue != null
                && value.FieldKey == PortalRecipientFieldKey)
            .Where(value => identities.Contains(value.FieldValue!.Trim().ToLower()))
            .Select(value => value.DocumentRecordId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (candidateRecordIds.Count == 0)
        {
            return Ok(new { success = true, data = Array.Empty<object>() });
        }

        var records = await _db.CentralDocumentRecords
            .AsNoTracking()
            .Include(record => record.Versions.Where(version => !version.IsDeleted))
            .Include(record => record.MetadataValues.Where(value => !value.IsDeleted))
            .Where(record => record.TenantId == tenantId
                && !record.IsDeleted
                && candidateRecordIds.Contains(record.Id)
                && record.SourceModule == "Estate"
                && record.LifecycleStatus == "Dispatched"
                && record.RepositoryStatus == "Linked")
            .OrderByDescending(record => record.PublishedAt ?? record.UpdatedAt ?? record.CreatedAt)
            .Take(100)
            .ToListAsync(cancellationToken);

        var data = records
            .Select(record =>
            {
                var version = record.Versions
                    .OrderByDescending(item => item.PublishedAt ?? item.UpdatedAt ?? item.CreatedAt)
                    .FirstOrDefault(item => !string.IsNullOrWhiteSpace(item.RenditionPath)
                        || !string.IsNullOrWhiteSpace(item.RepositoryPath));
                var contentUrl = $"/api/estate/external/documents/{record.Id}/content";
                var hasFile = !string.IsNullOrWhiteSpace(version?.RenditionPath)
                    || !string.IsNullOrWhiteSpace(version?.RepositoryPath)
                    || !string.IsNullOrWhiteSpace(record.RepositoryPath);

                return new
                {
                    record.Id,
                    documentRecordId = record.Id,
                    versionId = version?.Id,
                    record.DocumentReference,
                    record.Title,
                    record.SourceLabel,
                    record.SourceEntityType,
                    record.SourceRecordReference,
                    record.LifecycleStatus,
                    record.VersionStatus,
                    record.PublishedAt,
                    record.CreatedAt,
                    fileName = version?.FileName,
                    repositoryPath = hasFile ? contentUrl : null,
                    renditionPath = hasFile && IsPdfDocument(version?.ContentType, version?.FileName, version?.RenditionPath ?? version?.RepositoryPath ?? record.RepositoryPath) ? contentUrl : null,
                    contentType = ResolveExternalContentType(version, record),
                    version = version?.VersionNumber,
                    dispatchChannel = MetadataValue(record, "dispatchchannel"),
                    dispatchReference = MetadataValue(record, "dispatchreference"),
                    dispatchedAt = MetadataValue(record, "dispatchedat")
                };
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.renditionPath)
                || !string.IsNullOrWhiteSpace(item.repositoryPath))
            .ToList();

        return Ok(new { success = true, data });
    }

    [HttpGet("{id:guid}/content")]
    public async Task<IActionResult> GetMyEstateDocumentContent(Guid id, CancellationToken cancellationToken)
    {
        var record = await LoadAuthorizedExternalEstateDocumentAsync(id, cancellationToken);
        if (record is null)
        {
            return NotFound(new { success = false, message = "Estate document was not found." });
        }

        var version = SelectExternalDocumentVersion(record);
        var filePath = SelectExternalDocumentPath(record, version);
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return NotFound(new { success = false, message = "Estate document file was not found." });
        }

        try
        {
            var stream = await _fileStorageService.DownloadFileAsync(filePath, version?.FileUploadRecordId ?? record.Id);
            var fileName = SafeDownloadFileName(version?.FileName, record.DocumentReference);
            var contentType = ResolveExternalContentType(version, record);
            Response.Headers["Content-Disposition"] = $"inline; filename=\"{fileName}\"";
            return File(stream, contentType);
        }
        catch (FileNotFoundException)
        {
            return NotFound(new { success = false, message = "Estate document file was not found." });
        }
    }

    [HttpGet("/api/estate/external/request-types")]
    public IActionResult GetRequestTypes()
    {
        return Ok(new
        {
            success = true,
            data = ExternalRequestDefinitions.Select(definition => new
            {
                definition.Code,
                definition.Title,
                definition.Module,
                definition.EntityType,
                definition.Category
            })
        });
    }

    [HttpGet("/api/estate/external/requests")]
    public async Task<IActionResult> GetMyRequests(CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        var userId = GetUserId();
        if (tenantId == Guid.Empty || userId is null)
        {
            return Ok(new { success = true, data = Array.Empty<object>() });
        }

        var requests = await _db.ProcedureCases
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId
                && !item.IsDeleted
                && item.OpenedById == userId.Value
                && (item.SourceDepartment == "External Portal"
                    || item.SourceDepartment == "External Portal - Estate Services"
                    || item.SourceDepartment == "External Portal - Estate Listings"))
            .OrderByDescending(item => item.UpdatedAt ?? item.CreatedAt)
            .Take(100)
            .Select(item => new
            {
                item.Id,
                item.Module,
                item.EntityType,
                item.Title,
                item.ReferenceNumber,
                item.ApplicantName,
                item.Status,
                item.CurrentStageName,
                item.CurrentAssignedRole,
                item.CreatedAt,
                item.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(new { success = true, data = requests });
    }

    [HttpPost("/api/estate/external/requests")]
    public async Task<IActionResult> CreateRequest(
        [FromBody] CreateExternalEstateServiceRequest request,
        CancellationToken cancellationToken)
    {
        var definition = ExternalRequestDefinitions.FirstOrDefault(item =>
            string.Equals(item.Code, request.RequestType, StringComparison.OrdinalIgnoreCase));

        if (definition is null)
        {
            return BadRequest(new { success = false, message = "Unsupported Estate service request type." });
        }

        var reference = BuildExternalReference("PORTAL");
        var applicantName = string.IsNullOrWhiteSpace(request.ApplicantName)
            ? _currentUserService.UserName
            : request.ApplicantName.Trim();
        var contact = string.IsNullOrWhiteSpace(request.Contact)
            ? _currentUserService.Email ?? _currentUserService.UserName
            : request.Contact.Trim();
        var fieldValues = BuildFieldValues(definition, request, reference, contact);

        try
        {
            var created = await _procedureCaseService.CreateCaseAsync(new CreateProcedureCaseRequest(
                definition.Module,
                definition.EntityType,
                $"{definition.Title} - {applicantName}",
                reference,
                applicantName,
                "External Portal - Estate Services",
                DateTime.UtcNow,
                request.Description,
                fieldValues));

            await NotifyExternalServiceRequestAsync(definition, created.Id, created.ReferenceNumber ?? reference, applicantName, cancellationToken);

            return Ok(new
            {
                success = true,
                data = new
                {
                    created.Id,
                    created.Module,
                    created.EntityType,
                    created.Title,
                    created.ReferenceNumber,
                    created.Status,
                    created.CurrentStageName,
                    created.CurrentAssignedRole,
                    CreatedAt = DateTime.UtcNow
                }
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpGet("/api/estate/external/listings")]
    public async Task<IActionResult> GetListings(
        [FromQuery] string? location = null,
        [FromQuery] string? listingType = null,
        [FromQuery] string? search = null,
        [FromQuery] int take = 100,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
        {
            return Ok(new { success = true, data = Array.Empty<object>() });
        }

        var normalizedLocation = Normalize(location);
        var normalizedSearch = Normalize(search);
        var normalizedListingType = NormalizeListingType(listingType);
        var limit = Math.Clamp(take <= 0 ? 100 : take, 1, 200);

        var query = WhereExternallyAvailableListings(_db.EstateManagedAssets
            .AsNoTracking()
            .Include(asset => asset.Documents.Where(document => !document.IsDeleted && document.IsListingImage)))
            .Where(asset => asset.TenantId == tenantId
                && (asset.AssetType == EstateManagedAssetType.Land
                    || asset.AssetType == EstateManagedAssetType.Property
                    || asset.AssetType == EstateManagedAssetType.Facility));

        if (normalizedListingType != null)
        {
            query = query.Where(asset => asset.ExternalListingType == normalizedListingType
                || asset.ExternalListingType == "SaleAndRent");
        }

        if (normalizedLocation != null)
        {
            query = query.Where(asset =>
                (asset.Location != null && asset.Location.ToLower().Contains(normalizedLocation))
                || (asset.Town != null && asset.Town.ToLower().Contains(normalizedLocation))
                || (asset.District != null && asset.District.ToLower().Contains(normalizedLocation))
                || (asset.Region != null && asset.Region.ToLower().Contains(normalizedLocation)));
        }

        if (normalizedSearch != null)
        {
            query = query.Where(asset =>
                (asset.AssetCode != null && asset.AssetCode.ToLower().Contains(normalizedSearch))
                || (asset.Name != null && asset.Name.ToLower().Contains(normalizedSearch))
                || (asset.Description != null && asset.Description.ToLower().Contains(normalizedSearch))
                || (asset.Location != null && asset.Location.ToLower().Contains(normalizedSearch))
                || (asset.Town != null && asset.Town.ToLower().Contains(normalizedSearch))
                || (asset.District != null && asset.District.ToLower().Contains(normalizedSearch))
                || (asset.ProjectTitle != null && asset.ProjectTitle.ToLower().Contains(normalizedSearch))
                || (asset.UnitType != null && asset.UnitType.ToLower().Contains(normalizedSearch)));
        }

        // Estate external portal: apply all listing/search filters before paging published inventory.
        var filtered = await query
            .OrderByDescending(asset => asset.ExternalPublishedAt ?? asset.UpdatedAt ?? asset.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        var listings = filtered
            .Select(ToExternalListingDto)
            .ToList();

        return Ok(new { success = true, data = listings });
    }

    [HttpGet("/api/estate/external/listings/{listingId:guid}/images/{documentId:guid}")]
    public async Task<IActionResult> GetListingImage(Guid listingId, Guid documentId, CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        var externallyAvailableAssetIds = WhereExternallyAvailableListings(
                _db.EstateManagedAssets.AsNoTracking())
            .Where(item => item.TenantId == tenantId)
            .Select(item => item.Id);
        var document = await _db.EstateManagedAssetDocuments
            .AsNoTracking()
            .Include(item => item.EstateManagedAsset)
            .FirstOrDefaultAsync(item => item.Id == documentId
                && item.EstateManagedAssetId == listingId
                && item.TenantId == tenantId
                && !item.IsDeleted
                && item.IsListingImage
                && externallyAvailableAssetIds.Contains(item.EstateManagedAssetId), cancellationToken);

        if (document == null)
        {
            return NotFound(new { success = false, message = "Listing image was not found." });
        }

        var stream = await _fileStorageService.DownloadFileAsync(document.FilePath, document.Id);
        return File(stream, document.ContentType ?? "application/octet-stream", document.FileName);
    }

    [HttpPost("/api/estate/external/listings/{listingId:guid}/requests")]
    public async Task<IActionResult> CreateListingRequest(
        Guid listingId,
        [FromBody] CreateExternalListingRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        var asset = await WhereExternallyAvailableListings(_db.EstateManagedAssets.AsNoTracking())
            .FirstOrDefaultAsync(item => item.Id == listingId
                && item.TenantId == tenantId
                && (item.AssetType == EstateManagedAssetType.Land
                    || item.AssetType == EstateManagedAssetType.Property
                    || item.AssetType == EstateManagedAssetType.Facility), cancellationToken);

        if (asset == null)
        {
            return NotFound(new { success = false, message = "Listing was not found or is not available." });
        }

        var requestType = NormalizeListingRequestType(request.RequestType, asset.ExternalListingType);
        if (requestType == "Purchase" && request.OfferAmount is not > 0)
        {
            return BadRequest(new { success = false, message = "Enter a positive bid amount for this land sale." });
        }
        var applicantName = string.IsNullOrWhiteSpace(request.ApplicantName)
            ? _currentUserService.UserName
            : request.ApplicantName.Trim();
        var contact = string.IsNullOrWhiteSpace(request.Contact)
            ? _currentUserService.Email ?? _currentUserService.UserName
            : request.Contact.Trim();
        var reference = BuildExternalReference("LISTING");
        var requestLabel = requestType == "Purchase" ? "Purchase bid" : "Lease request";
        var description = string.IsNullOrWhiteSpace(request.Message)
            ? $"External portal {requestLabel.ToLowerInvariant()} for {asset.AssetCode} - {asset.Name}."
            : request.Message.Trim();

        var fieldValues = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["sourceLabel"] = "Source: External Portal -> Estate / Property Management",
            ["sourceSystem"] = "External Portal",
            ["sourceWorkspace"] = "Customer Sale / Rent Listings",
            ["listingRequestType"] = requestType,
            ["assetId"] = asset.Id.ToString(),
            ["assetCode"] = asset.AssetCode,
            ["propertyNumber"] = asset.AssetCode,
            ["propertyUnit"] = asset.ProjectUnitCode ?? asset.AssetCode,
            ["propertyName"] = asset.Name,
            ["location"] = asset.Location,
            ["locationDetail"] = FirstNonBlank(asset.Location, asset.Town, asset.District, asset.Region),
            ["applicantName"] = applicantName,
            ["requester"] = applicantName,
            ["requesterType"] = "External customer",
            ["contactReference"] = contact,
            ["listingPrice"] = asset.ExternalListingPrice?.ToString("0.##"),
            ["salePrice"] = asset.ExternalSalePrice?.ToString("0.##"),
            ["monthlyRent"] = asset.ExternalMonthlyRent?.ToString("0.##"),
            ["leaseTermMonths"] = asset.ExternalLeaseTermMonths?.ToString(),
            ["listingCurrency"] = asset.ExternalListingCurrency,
            ["listingType"] = asset.ExternalListingType,
            ["offerAmount"] = request.OfferAmount?.ToString("0.##"),
            ["offerCurrency"] = asset.ExternalListingCurrency,
            ["notes"] = description
        };

        try
        {
            var created = await _procedureCaseService.CreateCaseAsync(new CreateProcedureCaseRequest(
                "PropertyManagement",
                "EstatePropertyManagementOccupancyAvailability",
                $"{requestLabel} - {asset.Name}",
                reference,
                applicantName,
                "External Portal - Estate Listings",
                DateTime.UtcNow,
                description,
                fieldValues));

            await NotifyListingRequestAsync(asset, created.Id, created.ReferenceNumber ?? reference, requestType, applicantName, cancellationToken);

            return Ok(new
            {
                success = true,
                data = new
                {
                    created.Id,
                    created.Module,
                    created.EntityType,
                    created.Title,
                    created.ReferenceNumber,
                    created.Status,
                    created.CurrentStageName,
                    created.CurrentAssignedRole,
                    CreatedAt = DateTime.UtcNow
                }
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    private HashSet<string> BuildIdentityTerms()
    {
        var terms = new[]
            {
                _currentUserService.Email,
                _currentUserService.UserName
            }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim().ToLowerInvariant());

        return terms.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string? MetadataValue(CentralDocumentRecord record, string fieldKey) =>
        record.MetadataValues
            .FirstOrDefault(value => string.Equals(value.FieldKey, fieldKey, StringComparison.OrdinalIgnoreCase))
            ?.FieldValue;

    private Guid? GetUserId()
        => Guid.TryParse(_currentUserService.UserId, out var userId) ? userId : null;

    private async Task<CentralDocumentRecord?> LoadAuthorizedExternalEstateDocumentAsync(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        var identities = BuildIdentityTerms();
        if (tenantId == Guid.Empty || identities.Count == 0)
        {
            return null;
        }

        var isRecipient = await _db.CentralDocumentMetadataValues
            .AsNoTracking()
            .AnyAsync(value => value.TenantId == tenantId
                && !value.IsDeleted
                && value.DocumentRecordId == id
                && value.FieldValue != null
                && value.FieldKey == PortalRecipientFieldKey
                && identities.Contains(value.FieldValue!.Trim().ToLower()), cancellationToken);

        if (!isRecipient)
        {
            return null;
        }

        return await _db.CentralDocumentRecords
            .AsNoTracking()
            .Include(record => record.Versions.Where(version => !version.IsDeleted))
            .FirstOrDefaultAsync(record => record.TenantId == tenantId
                && !record.IsDeleted
                && record.Id == id
                && record.SourceModule == "Estate"
                && record.LifecycleStatus == "Dispatched"
                && record.RepositoryStatus == "Linked", cancellationToken);
    }

    private static CentralDocumentVersion? SelectExternalDocumentVersion(CentralDocumentRecord record)
    {
        var externallyReleasedVersions = record.Versions
            .Where(version => IsExternalDocumentVersionStatus(version.Status) && HasExternalDocumentVersionPath(version))
            .ToList();

        if (!string.IsNullOrWhiteSpace(record.CurrentVersion))
        {
            var current = externallyReleasedVersions.FirstOrDefault(version =>
                string.Equals(version.VersionNumber, record.CurrentVersion, StringComparison.OrdinalIgnoreCase));
            if (current is not null)
            {
                return current;
            }
        }

        // Estate external portal: dispatched recipients can only receive the version that was current/published externally.
        return externallyReleasedVersions
            .OrderByDescending(item => item.PublishedAt ?? item.UpdatedAt ?? item.CreatedAt)
            .FirstOrDefault();
    }

    private static bool IsExternalDocumentVersionStatus(string? status)
        => string.Equals(status, "Current", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "Published", StringComparison.OrdinalIgnoreCase);

    private static bool HasExternalDocumentVersionPath(CentralDocumentVersion version)
        => !string.IsNullOrWhiteSpace(version.RenditionPath)
            || !string.IsNullOrWhiteSpace(version.RepositoryPath);

    private static string? SelectExternalDocumentPath(CentralDocumentRecord record, CentralDocumentVersion? version)
    {
        if (!string.IsNullOrWhiteSpace(version?.RenditionPath))
        {
            return version.RenditionPath;
        }

        if (!string.IsNullOrWhiteSpace(version?.RepositoryPath))
        {
            return version.RepositoryPath;
        }

        return string.Equals(record.VersionStatus, "Current", StringComparison.OrdinalIgnoreCase)
            || string.Equals(record.VersionStatus, "Published", StringComparison.OrdinalIgnoreCase)
            ? record.RepositoryPath
            : null;
    }

    private static string ResolveExternalContentType(CentralDocumentVersion? version, CentralDocumentRecord record)
    {
        if (!string.IsNullOrWhiteSpace(version?.RenditionPath))
        {
            return "application/pdf";
        }

        if (!string.IsNullOrWhiteSpace(version?.ContentType))
        {
            return version.ContentType;
        }

        var path = version?.RepositoryPath ?? record.RepositoryPath ?? version?.FileName;
        return IsPdfDocument(null, version?.FileName, path) ? "application/pdf" : "application/octet-stream";
    }

    private static bool IsPdfDocument(string? contentType, string? fileName, string? path)
        => contentType?.Contains("pdf", StringComparison.OrdinalIgnoreCase) == true
            || fileName?.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) == true
            || path?.Contains(".pdf", StringComparison.OrdinalIgnoreCase) == true;

    private static string SafeDownloadFileName(string? fileName, string fallback)
    {
        var candidate = string.IsNullOrWhiteSpace(fileName) ? $"{fallback}.pdf" : fileName.Trim();
        foreach (var invalidChar in Path.GetInvalidFileNameChars())
        {
            candidate = candidate.Replace(invalidChar, '_');
        }

        return candidate;
    }

    private async Task NotifyExternalServiceRequestAsync(
        ExternalEstateRequestDefinition definition,
        Guid caseId,
        string referenceNumber,
        string? applicantName,
        CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        var sourceLabel = definition.Module == "Facilities"
            ? "Source: External Portal -> Estate / Facilities"
            : "Source: External Portal -> Estate";
        var roles = definition.Module == "Facilities"
            ? new[] { "Facilities Manager", "Estate Manager", "Estate Officer" }
            : new[] { "Estate Manager", "Estate Officer", "Land Registry Officer", "Records Officer" };

        try
        {
            await RoleNotificationDispatcher.NotifyRolesAsync(
                _db,
                _notificationService,
                tenantId,
                GetUserId(),
                roles,
                "External Estate request submitted",
                $"{definition.Title} request {referenceNumber} was submitted by {applicantName ?? "an external customer"}.",
                "estate.external.request",
                "ProcedureCase",
                caseId,
                definition.Module == "Facilities"
                    ? $"/estate/facilities/{definition.EntityType}"
                    : $"/estate/{definition.EntityType}",
                new Dictionary<string, object>
                {
                    ["sourceLabel"] = sourceLabel,
                    ["sourceModule"] = definition.Module,
                    ["requestType"] = definition.Code,
                    ["entityType"] = definition.EntityType,
                    ["referenceNumber"] = referenceNumber
                },
                cancellationToken);
        }
        catch
        {
            // Notification delivery must not block customer request submission.
        }
    }

    private async Task NotifyListingRequestAsync(
        EstateManagedAsset asset,
        Guid caseId,
        string referenceNumber,
        string requestType,
        string? applicantName,
        CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;

        try
        {
            await RoleNotificationDispatcher.NotifyRolesAsync(
                _db,
                _notificationService,
                tenantId,
                GetUserId(),
                new[] { "Property Manager", "Estate Manager", "Estate Officer" },
                "External property listing request submitted",
                $"{requestType} request {referenceNumber} was submitted for {asset.AssetCode} - {asset.Name} by {applicantName ?? "an external customer"}.",
                "estate.property.listing-request",
                "ProcedureCase",
                caseId,
                "/estate/property-management/EstatePropertyManagementOccupancyAvailability",
                new Dictionary<string, object>
                {
                    ["sourceLabel"] = "Source: External Portal -> Estate / Property Management",
                    ["sourceModule"] = "PropertyManagement",
                    ["assetId"] = asset.Id,
                    ["assetCode"] = asset.AssetCode,
                    ["listingType"] = asset.ExternalListingType,
                    ["requestType"] = requestType,
                    ["referenceNumber"] = referenceNumber
                },
                cancellationToken);
        }
        catch
        {
            // Notification delivery must not block customer request submission.
        }
    }

    private static object ToExternalListingDto(EstateManagedAsset asset)
    {
        var image = asset.Documents
            .Where(document => document.IsListingImage)
            .OrderByDescending(document => document.IsPrimaryListingImage)
            .ThenByDescending(document => document.CreatedAt)
            .FirstOrDefault();

        return new
        {
            asset.Id,
            asset.AssetCode,
            asset.Name,
            asset.AssetType,
            asset.Status,
            asset.Description,
            asset.Location,
            asset.Region,
            asset.District,
            asset.Town,
            asset.BlockName,
            asset.FloorLabel,
            asset.UnitType,
            asset.AreaSquareMeters,
            asset.AreaValue,
            asset.AreaUnit,
            asset.ExternalListingType,
            asset.ExternalListingPrice,
            asset.ExternalSalePrice,
            asset.ExternalMonthlyRent,
            asset.ExternalLeaseTermMonths,
            asset.ExternalListingCurrency,
            asset.ExternalListingNotes,
            asset.ExternalPublishedAt,
            PrimaryImageDocumentId = image?.Id,
            PrimaryImageUrl = image is null ? null : $"/estate/external/listings/{asset.Id}/images/{image.Id}",
            SourceLabel = "Source: Estate / Property Management -> External Portal"
        };
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();

    private static string? NormalizeListingType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "sale" => "Sale",
            "rent" or "lease" => "Rent",
            "saleandrent" or "sale and rent" or "sale/rent" or "both" => "SaleAndRent",
            _ => null
        };
    }

    private static string NormalizeListingRequestType(string? requestedType, string listingType)
    {
        var normalized = NormalizeListingType(requestedType);
        if (normalized is null)
        {
            normalized = listingType == "SaleAndRent" ? "Rent" : listingType;
        }

        if (listingType != "SaleAndRent" && normalized != listingType)
        {
            normalized = listingType;
        }

        return normalized == "Sale" ? "Purchase" : "Lease";
    }

    private static IQueryable<EstateManagedAsset> WhereExternallyAvailableListings(IQueryable<EstateManagedAsset> query)
        // Estate external portal: keep GET listings and POST requests on the same availability gate so stale listing IDs cannot start procedures.
        => query.Where(asset => !asset.IsDeleted
            && asset.IsPublishedToExternalPortal
            && asset.ExternalListingStatus == "Published"
            && (asset.Status == EstateManagedAssetStatus.Available || asset.Status == EstateManagedAssetStatus.LandBank)
            && (asset.ExternalListingType == "Sale"
                || ((asset.ExternalListingType == "Rent"
                        || asset.ExternalListingType == "SaleAndRent")
                    && asset.ExternalMonthlyRent.HasValue
                    && asset.ExternalMonthlyRent > 0
                    && asset.ExternalLeaseTermMonths.HasValue
                    && asset.ExternalLeaseTermMonths > 0))
            && ((asset.AssetType == EstateManagedAssetType.Land
                    && asset.Status == EstateManagedAssetStatus.LandBank
                    && !asset.ProjectId.HasValue
                    && !asset.IsReadyForProjectManagement
                    && asset.BoundaryVerified
                    && asset.Demarcations.Any(item => !item.IsDeleted)
                    && !asset.Demarcations.Any(item => !item.IsDeleted && !item.BoundaryVerified))
                || ((asset.AssetType == EstateManagedAssetType.Property
                        || asset.AssetType == EstateManagedAssetType.Facility)
                    && asset.SourceType == EstateManagedAssetSourceType.ProjectUnit
                    && asset.IsPublishedFromProject
                    && asset.Status == EstateManagedAssetStatus.Available)));

    private static string? FirstNonBlank(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private static string BuildExternalReference(string prefix)
        => $"{prefix}-{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}".ToUpperInvariant();

    private static IDictionary<string, string?> BuildFieldValues(
        ExternalEstateRequestDefinition definition,
        CreateExternalEstateServiceRequest request,
        string reference,
        string? contact)
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["referenceNumber"] = reference,
            ["applicantName"] = request.ApplicantName,
            ["propertyNumber"] = request.PropertyReference,
            ["propertyUnit"] = request.PropertyReference,
            ["location"] = request.Location,
            ["locationDetail"] = request.Location,
            ["sourceLabel"] = definition.Module == "Facilities" ? "Source: External Portal -> Estate / Facilities" : "Source: External Portal -> Estate",
            ["sourceSystem"] = "External Portal",
            ["sourceWorkspace"] = "Estate Services",
            ["contactReference"] = contact,
            ["notes"] = request.Description,
            ["issueDescription"] = request.Description,
            ["complaintDescription"] = request.Description,
            ["serviceImpact"] = request.ServiceImpact,
            ["priority"] = request.Priority,
            ["requester"] = request.ApplicantName,
            ["requesterType"] = "Tenant / occupant",
            ["complainantName"] = request.ApplicantName,
            ["complainantType"] = "Client",
            ["issueType"] = request.Category,
            ["complaintCategory"] = request.Category,
            ["targetDate"] = request.TargetDate?.ToString("yyyy-MM-dd"),
            ["schedule"] = ResolveEstateSchedule(definition),
            ["procedureType"] = definition.Title
        };

        foreach (var item in request.AdditionalValues ?? new Dictionary<string, string?>())
        {
            if (!string.IsNullOrWhiteSpace(item.Key))
            {
                values[item.Key.Trim()] = item.Value;
            }
        }

        return values;
    }

    private static string ResolveEstateSchedule(ExternalEstateRequestDefinition definition)
        => definition.EntityType switch
        {
            "EstateFacilityMaintenance" or "EstateFacilityComplaint" => "Facilities",
            "EstateSearchApplication" or "EstateCertifiedTrueCopy" or "EstateRecordAmendment" => "Records",
            "EstateServicedPlotAllocation" => "Serviced Plots",
            "EstateLandsPartiallyServiced" or "EstateAdditionalLand" or "EstateChangeOfUse" or "EstateTransfer" or "EstateAssignment" or "EstateMortgageConsent" or "EstateLeasePreparation" or "EstateLeaseRenewal" => "Lands / Partially Serviced",
            "EstateHousingHomeOwnership" => "Housing",
            "EstateTraditionalLands" => "Traditional Lands",
            "EstateTenancyRegularisation" => "Regularisation",
            _ => definition.Category
        };

    private static readonly ExternalEstateRequestDefinition[] ExternalRequestDefinitions =
    [
        new("maintenance", "Maintenance Request", "Facilities", "EstateFacilityMaintenance", "Maintenance"),
        new("complaint", "Facilities Complaint", "Facilities", "EstateFacilityComplaint", "Complaint"),
        new("searchApplication", "Search Application", "Estate", "EstateSearchApplication", "Estate records"),
        new("changeAddress", "Change of Address", "Estate", "EstateRecordAmendment", "Estate records"),
        new("certifiedTrueCopy", "Certified True Copy", "Estate", "EstateCertifiedTrueCopy", "Estate records"),
        new("jointOwnership", "Joint Ownership / Addition of Name", "Estate", "EstateJointOwnership", "Ownership"),
        new("transfer", "Transfer / Portion Transfer", "Estate", "EstateTransfer", "Transfer"),
        new("assignment", "Assignment Consent", "Estate", "EstateAssignment", "Assignment"),
        new("mortgageConsent", "Consent to Mortgage / Mortgage in Principle", "Estate", "EstateMortgageConsent", "Mortgage"),
        new("leaseDocument", "Lease Document Preparation", "Estate", "EstateLeasePreparation", "Lease"),
        new("additionalLand", "Additional Land Application", "Estate", "EstateAdditionalLand", "Allocation"),
        new("changeOfUse", "Change of Land Use", "Estate", "EstateChangeOfUse", "Land use"),
        new("leaseRenewal", "Lease Renewal", "Estate", "EstateLeaseRenewal", "Lease"),
        new("landApplication", "Land / Partially Serviced Plot Application", "Estate", "EstateLandsPartiallyServiced", "Allocation"),
        new("traditionalLand", "Traditional Land Documentation", "Estate", "EstateTraditionalLands", "Traditional Lands"),
        new("tenancyRecognition", "Recognition of Tenancy", "Estate", "EstateHousingHomeOwnership", "Housing"),
        new("hosConversion", "Conversion of Rental Unit to HOS", "Estate", "EstateHousingHomeOwnership", "Housing"),
        new("regularisation", "Tenancy Regularisation", "Estate", "EstateTenancyRegularisation", "Regularisation"),
        new("rightOfEntry", "Right of Entry / Allocation Follow-up", "Estate", "EstateServicedPlotAllocation", "Allocation")
    ];
}

public sealed record CreateExternalEstateServiceRequest(
    string RequestType,
    string? ApplicantName,
    string? Contact,
    string? PropertyReference,
    string? Location,
    string? Category,
    string? Priority,
    string? ServiceImpact,
    DateTime? TargetDate,
    string? Description,
    IDictionary<string, string?>? AdditionalValues);

public sealed record CreateExternalListingRequest(
    string? RequestType,
    string? ApplicantName,
    string? Contact,
    decimal? OfferAmount,
    string? Message);

public sealed record ExternalEstateRequestDefinition(
    string Code,
    string Title,
    string Module,
    string EntityType,
    string Category);
