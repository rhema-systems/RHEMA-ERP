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
    private static readonly string[] PortalRecipientFieldKeys =
    [
        "dispatchedto",
        "applicantname",
        "applicantemail",
        "email",
        "recipient"
    ];

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
                && PortalRecipientFieldKeys.Contains(value.FieldKey))
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

                return new
                {
                    record.Id,
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
                    repositoryPath = version?.RepositoryPath ?? record.RepositoryPath,
                    renditionPath = version?.RenditionPath,
                    contentType = version?.ContentType,
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
                    || item.SourceDepartment == "External Portal - Estate Services"))
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

        var reference = $"PORTAL-{DateTime.UtcNow:yyyyMMddHHmmss}";
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

            await NotifyExternalServiceRequestAsync(definition, created.Id, created.ReferenceNumber, applicantName, cancellationToken);

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

        var assets = await _db.EstateManagedAssets
            .AsNoTracking()
            .Include(asset => asset.Documents.Where(document => !document.IsDeleted && document.IsListingImage))
            .Where(asset => asset.TenantId == tenantId
                && !asset.IsDeleted
                && asset.IsPublishedToExternalPortal
                && asset.ExternalListingStatus == "Published"
                && (asset.Status == EstateManagedAssetStatus.Available || asset.Status == EstateManagedAssetStatus.LandBank)
                && (asset.AssetType == EstateManagedAssetType.Land
                    || asset.AssetType == EstateManagedAssetType.Property
                    || asset.AssetType == EstateManagedAssetType.Facility))
            .OrderByDescending(asset => asset.ExternalPublishedAt ?? asset.UpdatedAt ?? asset.CreatedAt)
            .Take(500)
            .ToListAsync(cancellationToken);

        var filtered = assets
            .Where(asset => normalizedListingType is null
                || asset.ExternalListingType == normalizedListingType
                || asset.ExternalListingType == "SaleAndRent")
            .Where(asset => normalizedLocation is null
                || Contains(asset.Location, normalizedLocation)
                || Contains(asset.Town, normalizedLocation)
                || Contains(asset.District, normalizedLocation)
                || Contains(asset.Region, normalizedLocation))
            .Where(asset => normalizedSearch is null
                || Contains(asset.AssetCode, normalizedSearch)
                || Contains(asset.Name, normalizedSearch)
                || Contains(asset.Description, normalizedSearch)
                || Contains(asset.Location, normalizedSearch)
                || Contains(asset.Town, normalizedSearch)
                || Contains(asset.District, normalizedSearch)
                || Contains(asset.ProjectTitle, normalizedSearch)
                || Contains(asset.UnitType, normalizedSearch))
            .Take(limit)
            .Select(ToExternalListingDto)
            .ToList();

        return Ok(new { success = true, data = filtered });
    }

    [HttpGet("/api/estate/external/listings/{listingId:guid}/images/{documentId:guid}")]
    public async Task<IActionResult> GetListingImage(Guid listingId, Guid documentId, CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        var document = await _db.EstateManagedAssetDocuments
            .AsNoTracking()
            .Include(item => item.EstateManagedAsset)
            .FirstOrDefaultAsync(item => item.Id == documentId
                && item.EstateManagedAssetId == listingId
                && item.TenantId == tenantId
                && !item.IsDeleted
                && item.IsListingImage
                && item.EstateManagedAsset.IsPublishedToExternalPortal
                && item.EstateManagedAsset.ExternalListingStatus == "Published", cancellationToken);

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
        var asset = await _db.EstateManagedAssets
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == listingId
                && item.TenantId == tenantId
                && !item.IsDeleted
                && item.IsPublishedToExternalPortal
                && item.ExternalListingStatus == "Published", cancellationToken);

        if (asset == null)
        {
            return NotFound(new { success = false, message = "Listing was not found or is not available." });
        }

        var requestType = NormalizeListingRequestType(request.RequestType, asset.ExternalListingType);
        var applicantName = string.IsNullOrWhiteSpace(request.ApplicantName)
            ? _currentUserService.UserName
            : request.ApplicantName.Trim();
        var contact = string.IsNullOrWhiteSpace(request.Contact)
            ? _currentUserService.Email ?? _currentUserService.UserName
            : request.Contact.Trim();
        var reference = $"LISTING-{DateTime.UtcNow:yyyyMMddHHmmss}";
        var description = string.IsNullOrWhiteSpace(request.Message)
            ? $"External portal {requestType.ToLowerInvariant()} request for {asset.AssetCode} - {asset.Name}."
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
            ["listingCurrency"] = asset.ExternalListingCurrency,
            ["listingType"] = asset.ExternalListingType,
            ["notes"] = description
        };

        try
        {
            var created = await _procedureCaseService.CreateCaseAsync(new CreateProcedureCaseRequest(
                "PropertyManagement",
                "EstatePropertyManagementOccupancyAvailability",
                $"{requestType} request - {asset.Name}",
                reference,
                applicantName,
                "External Portal - Estate Listings",
                DateTime.UtcNow,
                description,
                fieldValues));

            await NotifyListingRequestAsync(asset, created.Id, created.ReferenceNumber, requestType, applicantName, cancellationToken);

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

    private static bool Contains(string? value, string search)
        => value?.Contains(search, StringComparison.OrdinalIgnoreCase) == true;

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

    private static string? FirstNonBlank(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

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
            ["schedule"] = "Facilities",
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

    private static readonly ExternalEstateRequestDefinition[] ExternalRequestDefinitions =
    [
        new("maintenance", "Maintenance Request", "Facilities", "EstateFacilityMaintenance", "Maintenance"),
        new("complaint", "Facilities Complaint", "Facilities", "EstateFacilityComplaint", "Complaint"),
        new("changeOfUse", "Change of Use / Record Amendment", "Estate", "EstateRecordAmendment", "Estate records"),
        new("certifiedTrueCopy", "Certified True Copy", "Estate", "EstateCertifiedTrueCopy", "Estate records"),
        new("searchApplication", "Search Application", "Estate", "EstateSearchApplication", "Estate records"),
        new("leaseRenewal", "Lease Renewal", "Estate", "EstateLeaseRenewal", "Lease"),
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
    string? Message);

public sealed record ExternalEstateRequestDefinition(
    string Code,
    string Title,
    string Module,
    string EntityType,
    string Category);
