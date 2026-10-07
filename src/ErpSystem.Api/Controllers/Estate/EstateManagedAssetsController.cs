using ErpSystem.Core.DTOs.Estate;
using ErpSystem.Core.DTOs.Procedures;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.Estate;
using ErpSystem.Core.Entities.Procedures;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Estate;
using ErpSystem.Core.Interfaces.Procedures;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Models;
using ErpSystem.Core.Services.Estate;
using ErpSystem.Api.Services.Estate;
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
    private readonly IProjectService _projectService;
    private readonly IProcedureCaseService _procedureCaseService;
    private readonly IFileStorageService _fileStorageService;
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;
    private readonly IEstateSalesListingApplicationHandoffService _salesListingApplicationHandoffService;

    public EstateManagedAssetsController(
        IEstateManagedAssetService managedAssetService,
        IProjectService projectService,
        IProcedureCaseService procedureCaseService,
        IFileStorageService fileStorageService,
        ApplicationDbContext db,
        ICurrentUserService currentUserService,
        IEstateSalesListingApplicationHandoffService salesListingApplicationHandoffService)
    {
        _managedAssetService = managedAssetService;
        _projectService = projectService;
        _procedureCaseService = procedureCaseService;
        _fileStorageService = fileStorageService;
        _db = db;
        _currentUserService = currentUserService;
        _salesListingApplicationHandoffService = salesListingApplicationHandoffService;
    }

    [HttpGet]
    public async Task<IActionResult> GetManagedAssets(
        [FromQuery] EstateManagedAssetType? assetType = null,
        [FromQuery] EstateManagedAssetStatus? status = null,
        [FromQuery] List<EstateManagedAssetStatus>? statuses = null,
        [FromQuery] string? search = null,
        [FromQuery] bool? availableForLease = null,
        [FromQuery] bool? availableForSale = null,
        [FromQuery] bool? portalListingCandidates = null,
        [FromQuery] string? externalListingStatus = null,
        [FromQuery] bool? publishedToExternalPortal = null,
        [FromQuery] bool includeLandDemarcations = false,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 100)
    {
        var assets = await _managedAssetService.GetManagedAssetsAsync(new EstateManagedAssetQuery
        {
            AssetType = assetType,
            Status = status,
            Statuses = statuses ?? new List<EstateManagedAssetStatus>(),
            Search = search,
            AvailableForLease = availableForLease,
            AvailableForSale = availableForSale,
            PortalListingCandidates = portalListingCandidates,
            ExternalListingStatus = externalListingStatus,
            PublishedToExternalPortal = publishedToExternalPortal,
            IncludeLandDemarcations = includeLandDemarcations,
            Skip = skip,
            Take = take
        });

        return Ok(new
        {
            success = true,
            data = assets
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetManagedAsset(Guid id)
    {
        if (!_currentUserService.TenantId.HasValue || _currentUserService.TenantId == Guid.Empty)
        {
            return Forbid();
        }

        // Use the register owner so tenant/deleted visibility and enrichment stay identical.
        var assets = await _managedAssetService.GetManagedAssetsAsync(new EstateManagedAssetQuery
        {
            AssetId = id,
            Take = 1
        });
        var asset = assets.SingleOrDefault();
        return asset is null
            ? NotFound(new { success = false, message = "Managed asset not found." })
            : Ok(new { success = true, data = asset });
    }

    [HttpPost("manual-land")]
    [Authorize(Roles = "admin,Admin,SystemAdmin,SuperAdmin,TenantAdmin,Estate Officer,Estate Manager,Land Registry Officer")]
    public async Task<IActionResult> CreateManualLand([FromBody] CreateManualExistingLandDto request)
    {
        if (request.IsReadyForProjectManagement && !await CanMarkReadyForProjectManagementAsync())
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
    public async Task<IActionResult> MarkReadyForProjectManagement(Guid id)
    {
        if (!await CanMarkReadyForProjectManagementAsync())
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

    [HttpGet("{id:guid}/demarcations")]
    public async Task<IActionResult> GetLandDemarcations(Guid id)
    {
        try
        {
            return Ok(new
            {
                success = true,
                data = await _managedAssetService.GetLandDemarcationsAsync(id)
            });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { success = false, message = ex.Message });
        }
    }

    [HttpGet("project-ready-demarcations")]
    public async Task<IActionResult> GetProjectReadyLandDemarcations([FromQuery] Guid? projectId = null)
    {
        if (projectId.HasValue)
        {
            try
            {
                // Reuse Project Management's tenant, membership, and role-aware view authorization
                // before exposing the selected parcel for a specific project.
                await _projectService.GetDevelopmentProfileAsync(projectId.Value);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { success = false, message = ex.Message });
            }
        }

        return Ok(new
        {
            success = true,
            data = await _managedAssetService.GetProjectReadyLandDemarcationsAsync(projectId)
        });
    }

    [HttpGet("portal-listing-demarcations")]
    public async Task<IActionResult> GetPortalListingDemarcations(
        [FromQuery] string? search = null,
        [FromQuery] string? externalListingStatus = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 300)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
        {
            return Ok(new { success = true, data = Array.Empty<object>() });
        }

        var normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim().ToLowerInvariant();
        var limit = Math.Clamp(take <= 0 ? 300 : take, 1, 500);
        var offset = Math.Max(0, skip);
        var query = _db.EstateLandDemarcations
            .AsNoTracking()
            .Include(item => item.EstateManagedAsset)
            .Where(item => item.TenantId == tenantId
                && !item.IsDeleted
                && item.EstateManagedAsset.TenantId == tenantId
                && !item.EstateManagedAsset.IsDeleted
                && item.EstateManagedAsset.AssetType == EstateManagedAssetType.Land
                && (item.IsPublishedToExternalPortal || item.ExternalListingType != "None"));

        if (externalListingStatus == "PendingPublication")
        {
            query = query.Where(item => !item.IsPublishedToExternalPortal
                && item.ExternalListingStatus == "Draft");
        }
        else if (externalListingStatus == "Draft")
        {
            query = query.Where(item => item.IsPublishedToExternalPortal
                && item.ExternalListingStatus == "Draft");
        }
        else if (!string.IsNullOrWhiteSpace(externalListingStatus))
        {
            query = query.Where(item => item.ExternalListingStatus == externalListingStatus);
        }

        if (normalizedSearch != null)
        {
            query = query.Where(item =>
                item.Description.ToLower().Contains(normalizedSearch)
                || (item.ChildFixedAssetReference != null
                    && item.ChildFixedAssetReference.ToLower().Contains(normalizedSearch))
                || (item.ParentLandAssetReference != null
                    && item.ParentLandAssetReference.ToLower().Contains(normalizedSearch))
                || item.EstateManagedAsset.AssetCode.ToLower().Contains(normalizedSearch)
                || item.EstateManagedAsset.Name.ToLower().Contains(normalizedSearch)
                || (item.EstateManagedAsset.Location != null && item.EstateManagedAsset.Location.ToLower().Contains(normalizedSearch)));
        }

        var demarcations = await query
            .OrderByDescending(item => item.ExternalPublishedAt ?? item.UpdatedAt ?? item.CreatedAt)
            .Skip(offset)
            .Take(limit)
            .ToListAsync();
        var squareMetersPerPlot = await EstateSettingsController.GetSquareMetersPerPlotAsync(
            _db,
            tenantId,
            HttpContext.RequestAborted);
        var candidates = demarcations
            .Select(item => new
            {
                ListingScope = "demarcation",
                ParentAssetId = item.EstateManagedAssetId,
                Id = item.Id,
                AssetCode = EstateLandDemarcationReference.DisplayReference(
                    item.ChildFixedAssetReference, item.EstateManagedAsset.AssetCode, item.DemarcationNumber),
                Name = EstateLandDemarcationReference.DisplayReference(
                    item.ChildFixedAssetReference, item.EstateManagedAsset.AssetCode, item.DemarcationNumber),
                Description = item.Description,
                Location = item.EstateManagedAsset.Location,
                Purpose = item.EstateManagedAsset.Purpose,
                ZoningClassification = item.EstateManagedAsset.ZoningClassification,
                PlanningComplianceStatus = item.EstateManagedAsset.PlanningComplianceStatus,
                GisLayerReference = item.EstateManagedAsset.GisLayerReference,
                GisProvider = item.EstateManagedAsset.GisProvider,
                GisFeatureId = item.EstateManagedAsset.GisFeatureId,
                GisSourceCrs = item.EstateManagedAsset.GisSourceCrs,
                GisSyncStatus = item.EstateManagedAsset.GisSyncStatus,
                GisLastSyncedAt = item.EstateManagedAsset.GisLastSyncedAt,
                BoundaryVerified = item.BoundaryVerified,
                BoundaryCoordinates = item.BoundaryCoordinates,
                SurveyPlanNumber = item.EstateManagedAsset.SurveyPlanNumber,
                MapSheetNumber = item.EstateManagedAsset.MapSheetNumber,
                CadastreDescription = item.EstateManagedAsset.CadastreDescription,
                Region = item.EstateManagedAsset.Region,
                District = item.EstateManagedAsset.District,
                Town = item.EstateManagedAsset.Town,
                AreaValue = item.AreaSquareFeet,
                AreaUnit = "SqFt",
                AreaSquareMeters = item.AreaSquareFeet / 10.7639m,
                SquareMetersPerPlot = squareMetersPerPlot,
                PlotEquivalentCount = CalculatePlotEquivalent(item.AreaSquareFeet / 10.7639m, squareMetersPerPlot),
                SurveyorName = item.EstateManagedAsset.SurveyorName,
                SurveyDate = item.EstateManagedAsset.SurveyDate,
                BeaconCount = item.BeaconCount,
                DemarcationCount = 0,
                VerifiedDemarcationCount = 0,
                IsReadyForProjectManagement = item.IsReadyForProjectManagement,
                AssetType = EstateManagedAssetType.Land,
                Status = item.EstateManagedAsset.Status,
                SourceType = item.EstateManagedAsset.SourceType,
                LandAcquisitionId = item.EstateManagedAsset.LandAcquisitionId,
                ProjectId = item.EstateManagedAsset.ProjectId,
                ProjectCode = item.EstateManagedAsset.ProjectCode,
                ProjectTitle = item.EstateManagedAsset.ProjectTitle,
                GroundRentPayable = item.GroundRentPayable,
                GroundRentRatePerAcre = item.GroundRentRatePerAcre,
                GroundRentComputed = item.GroundRentComputed,
                AreaSquareFeet = item.AreaSquareFeet,
                ValuationAmount = item.AllocatedCost,
                TargetSalePrice = item.TargetSalePrice,
                Currency = item.ExternalListingCurrency,
                IsAvailableForLease = item.ExternalListingType == "Rent"
                    || item.ExternalListingType == "Lease"
                    || item.ExternalListingType == "SaleAndRent"
                    || item.ExternalListingType == "SaleAndLease",
                IsAvailableForSale = item.ExternalListingType == "Sale"
                    || item.ExternalListingType == "SaleAndRent"
                    || item.ExternalListingType == "SaleAndLease",
                IsPublishedFromProject = item.EstateManagedAsset.IsPublishedFromProject,
                PublishedFromProjectAt = item.EstateManagedAsset.PublishedFromProjectAt,
                IsPublishedToExternalPortal = item.IsPublishedToExternalPortal,
                ExternalListingType = item.ExternalListingType,
                ExternalListingStatus = item.ExternalListingStatus,
                ExternalListingPrice = item.ExternalListingPrice,
                ExternalSalePrice = item.ExternalSalePrice,
                ExternalMonthlyRent = item.ExternalMonthlyRent,
                ExternalGroundRentRequired = item.ExternalGroundRentRequired,
                ExternalPremiumChargeRequired = item.ExternalPremiumChargeRequired,
                ExternalPremiumChargeAmount = item.ExternalPremiumChargeAmount,
                ExternalLeaseTermMonths = item.ExternalLeaseTermMonths,
                ExternalListingCurrency = item.ExternalListingCurrency,
                ExternalListingNotes = item.ExternalListingNotes,
                ExternalPublishedAt = item.ExternalPublishedAt,
                Notes = item.FixedAssetPostingStatus
            })
            .ToList();

        return Ok(new { success = true, data = candidates });
    }

    [HttpPost("{id:guid}/demarcations")]
    [Authorize(Roles = "admin,Admin,SystemAdmin,SuperAdmin,TenantAdmin,Estate Officer,Estate Manager,Land Registry Officer,Survey Officer")]
    public async Task<IActionResult> CreateLandDemarcation(Guid id, [FromBody] SaveEstateLandDemarcationDto request)
    {
        try
        {
            var demarcation = await _managedAssetService.CreateLandDemarcationAsync(id, request);
            return Ok(new { success = true, data = demarcation, message = "Land demarcation added." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPatch("{id:guid}/demarcations/{demarcationId:guid}")]
    [Authorize(Roles = "admin,Admin,SystemAdmin,SuperAdmin,TenantAdmin,Estate Officer,Estate Manager,Land Registry Officer,Survey Officer")]
    public async Task<IActionResult> UpdateLandDemarcation(
        Guid id,
        Guid demarcationId,
        [FromBody] SaveEstateLandDemarcationDto request)
    {
        try
        {
            var demarcation = await _managedAssetService.UpdateLandDemarcationAsync(id, demarcationId, request);
            return Ok(new { success = true, data = demarcation, message = "Land demarcation updated." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpDelete("{id:guid}/demarcations/{demarcationId:guid}")]
    [Authorize(Roles = "admin,Admin,SystemAdmin,SuperAdmin,TenantAdmin,Estate Officer,Estate Manager,Land Registry Officer,Survey Officer")]
    public async Task<IActionResult> DeleteLandDemarcation(Guid id, Guid demarcationId)
    {
        try
        {
            await _managedAssetService.DeleteLandDemarcationAsync(id, demarcationId);
            return Ok(new { success = true, message = "Land demarcation deleted." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPatch("{id:guid}/demarcations/{demarcationId:guid}/disposition")]
    [Authorize(Roles = "admin,Admin,SystemAdmin,SuperAdmin,TenantAdmin,Estate Officer,Estate Manager,Property Manager,Land Registry Officer,Survey Officer")]
    public async Task<IActionResult> UpdateLandDemarcationDisposition(
        Guid id,
        Guid demarcationId,
        [FromBody] UpdateEstateLandDemarcationDispositionDto request)
    {
        try
        {
            var demarcation = await _managedAssetService.UpdateLandDemarcationDispositionAsync(id, demarcationId, request);
            return Ok(new { success = true, data = demarcation, message = "Land demarcation status updated." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPatch("{id:guid}/demarcations/{demarcationId:guid}/costing")]
    [Authorize(Roles = "admin,Admin,SystemAdmin,SuperAdmin,TenantAdmin,Estate Officer,Estate Manager,Property Manager,Land Registry Officer,Survey Officer")]
    public async Task<IActionResult> UpdateLandDemarcationCosting(
        Guid id,
        Guid demarcationId,
        [FromBody] UpdateEstateLandDemarcationCostingDto request)
    {
        try
        {
            var demarcation = await _managedAssetService.UpdateLandDemarcationCostingAsync(id, demarcationId, request);
            return Ok(new { success = true, data = demarcation, message = "Land demarcation costing updated." });
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

    [HttpPatch("{id:guid}/register")]
    [Authorize(Roles = "admin,Admin,SystemAdmin,SuperAdmin,TenantAdmin,Estate Officer,Estate Manager,Property Manager,Land Registry Officer")]
    public async Task<IActionResult> UpdateRegister(
        Guid id,
        [FromBody] UpdateEstateManagedAssetRegisterDto request)
    {
        try
        {
            var asset = await _managedAssetService.UpdateRegisterAsync(id, request);
            return Ok(new { success = true, data = asset, message = "Estate register record updated." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPatch("{id:guid}/occupancy")]
    [Authorize(Roles = "admin,Admin,SystemAdmin,SuperAdmin,TenantAdmin,Estate Officer,Estate Manager,Property Manager,Land Registry Officer")]
    public async Task<IActionResult> UpdateOccupancy(
        Guid id,
        [FromBody] UpdateEstateManagedAssetOccupancyDto request)
    {
        try
        {
            var asset = await _managedAssetService.UpdateOccupancyAsync(id, request);
            return Ok(new { success = true, data = asset, message = "Occupancy and availability updated." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpGet("{id:guid}/completed-terminations")]
    [Authorize(Roles = "admin,Admin,SystemAdmin,SuperAdmin,TenantAdmin,Estate Officer,Estate Manager,Property Manager,Land Registry Officer")]
    public async Task<IActionResult> GetCompletedTerminations(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var asset = await _db.EstateManagedAssets.AsNoTracking().FirstOrDefaultAsync(item =>
            item.Id == id && item.TenantId == tenantId && !item.IsDeleted, cancellationToken);
        if (asset is null) return NotFound();

        var assetId = id.ToString();
        var customerId = asset.CustomerBusinessPartnerId?.ToString();
        var cases = await _db.ProcedureCases.AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted
                && item.Module == "Legal" && item.EntityType == "LegalTerminationRecognition"
                && item.Status == "Completed" && item.CompletedAt.HasValue
                && (!asset.DateOfTenancy.HasValue || item.CompletedAt >= asset.DateOfTenancy.Value)
                && item.Fields.Any(field => !field.IsDeleted
                    && field.Key == "estateManagedAssetId" && field.Value == assetId)
                && (customerId == null || item.Fields.Any(field => !field.IsDeleted
                    && field.Key == "customerReference" && field.Value == customerId)))
            .OrderByDescending(item => item.CompletedAt)
            .Select(item => new { item.Id, item.ReferenceNumber, item.Title, item.CompletedAt })
            .ToListAsync(cancellationToken);
        return Ok(new { success = true, data = cases });
    }

    [HttpPost("sales-handoffs/listing-applications")]
    [Authorize(Roles = "admin,Admin,SystemAdmin,SuperAdmin,TenantAdmin,Estate Officer,Estate Manager,Property Manager,Sales User,Sales Officer,Sales Manager")]
    public async Task<IActionResult> CreateListingApplicationFromSales(
        [FromBody] CreateEstateSalesListingApplicationHandoffDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _salesListingApplicationHandoffService.CreateAsync(GetTenantId(), new(
                request.ListingId,
                request.BusinessPartnerId,
                request.RequestType,
                request.SalesOpportunityId ?? Guid.Empty,
                request.SalesReference ?? string.Empty,
                request.AgreedAmount,
                request.RequestedLeaseTerm,
                request.SalesAmountPaid,
                request.SalesPaymentReference,
                request.Currency,
                request.SalesCompletedAt,
                request.Notes,
                ActorUserId: GetUserId()), cancellationToken);
            return Ok(new
            {
                success = true,
                data = result,
                message = result.AlreadyExists
                    ? "Estate listing application already exists for this Sales opportunity."
                    : "Estate listing application created from Sales handoff."
            });
        }
        catch (KeyNotFoundException ex) { return NotFound(new { success = false, message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { success = false, message = ex.Message }); }
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

        await using var stream = file.OpenReadStream();
        // PR review: managed-asset evidence and listing images are served through authorized endpoints, not static uploads.
        var upload = await _fileStorageService.UploadFileAsync(new FileUploadRequest
        {
            FileStream = stream,
            FileName = file.FileName,
            ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
            FileSize = file.Length,
            Category = "estate-managed-asset-documents",
            TenantId = tenantId.ToString(),
            OverwriteExisting = false
        });
        if (!upload.Success)
        {
            return BadRequest(new { success = false, message = upload.ErrorMessage ?? "Estate asset file upload failed." });
        }

        var document = await _managedAssetService.RegisterDocumentAsync(id, new RegisterEstateManagedAssetDocumentDto
        {
            FileName = file.FileName,
            FilePath = upload.FilePath,
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
            FileUploadRecordId = null,
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
        => _currentUserService.TenantId is { } tenantId && tenantId != Guid.Empty
            ? tenantId
            : throw new UnauthorizedAccessException("Tenant context is required.");

    private Guid? GetUserId()
        => Guid.TryParse(_currentUserService.UserId, out var userId) ? userId : null;

    private async Task<bool> CanMarkReadyForProjectManagementAsync()
    {
        if (_currentUserService.IsInRole("SuperAdmin") ||
            _currentUserService.IsInRole("TenantAdmin"))
        {
            return true;
        }

        var userId = GetUserId();
        if (!userId.HasValue)
        {
            return false;
        }

        return await _db.UserRoles
            .AsNoTracking()
            .Where(userRole => userRole.UserId == userId.Value)
            .SelectMany(userRole => userRole.Role.RolePermissions)
            .AnyAsync(rolePermission =>
                rolePermission.Permission.Name == "estate.land.project-readiness"
                || rolePermission.Permission.Name == "*");
    }

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

    private static string NormalizeSalesHandoffRequestType(string? requestedType, string listingType)
    {
        var normalized = string.IsNullOrWhiteSpace(requestedType)
            ? listingType
            : requestedType.Trim();
        normalized = normalized.Equals("Purchase", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("Buy", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("Sale", StringComparison.OrdinalIgnoreCase)
            ? "Purchase"
            : normalized.Equals("Lease", StringComparison.OrdinalIgnoreCase)
                ? "Lease"
                : normalized.Equals("Rent", StringComparison.OrdinalIgnoreCase)
                    || normalized.Equals("Rental", StringComparison.OrdinalIgnoreCase)
                    || normalized.Equals("Tenancy", StringComparison.OrdinalIgnoreCase)
                    ? "Rent"
                    : listingType switch
                    {
                        "Sale" => "Purchase",
                        "Lease" or "SaleAndLease" => "Lease",
                        "Rent" or "SaleAndRent" => "Rent",
                        _ => "Lease"
                    };

        if (listingType == "Sale" && normalized != "Purchase")
        {
            return "Purchase";
        }

        if (listingType == "Lease" && normalized != "Lease")
        {
            return "Lease";
        }

        if (listingType == "Rent" && normalized != "Rent")
        {
            return "Rent";
        }

        return normalized;
    }

    private static string BuildExternalReference(string prefix)
        => $"{prefix}-{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}".ToUpperInvariant();

    private static decimal? CalculatePlotEquivalent(decimal? areaSquareMeters, decimal? squareMetersPerPlot)
        => areaSquareMeters is > 0m && squareMetersPerPlot is > 0m
            ? Math.Round(areaSquareMeters.Value / squareMetersPerPlot.Value, 2, MidpointRounding.AwayFromZero)
            : null;

    private static object ToSalesHandoffCaseDto(ProcedureCase procedureCase) => new
    {
        procedureCase.Id,
        procedureCase.Module,
        procedureCase.EntityType,
        procedureCase.Title,
        procedureCase.ReferenceNumber,
        procedureCase.Status,
        procedureCase.CurrentStageName,
        procedureCase.CurrentAssignedRole,
        CreatedAt = procedureCase.CreatedAt
    };

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

public sealed record CreateEstateSalesListingApplicationHandoffDto(
    Guid ListingId,
    Guid BusinessPartnerId,
    string? RequestType,
    Guid? SalesOpportunityId,
    string? SalesReference,
    decimal? AgreedAmount,
    string? RequestedLeaseTerm,
    decimal? SalesAmountPaid,
    string? SalesPaymentReference,
    string? Currency,
    DateTime? SalesCompletedAt,
    string? Notes);
