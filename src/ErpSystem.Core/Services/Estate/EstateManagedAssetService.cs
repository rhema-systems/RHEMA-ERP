using ErpSystem.Core.DTOs.Estate;
using ErpSystem.Core.Entities.Estate;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Estate;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ErpSystem.Core.Services.Estate;

public class EstateManagedAssetService : IEstateManagedAssetService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;

    public EstateManagedAssetService(IUnitOfWork unitOfWork, ICurrentUserProvider currentUserProvider)
    {
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
    }

    public async Task<IReadOnlyList<EstateManagedAssetDto>> GetManagedAssetsAsync(EstateManagedAssetQuery query)
    {
        var take = Math.Clamp(query.Take <= 0 ? 100 : query.Take, 1, 500);
        var normalizedSearch = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();
        var excludedStatuses = query.ExcludedStatuses.Distinct().ToList();
        var assetsQuery = _unitOfWork.Repository<EstateManagedAsset>()
            .GetQueryable(item =>
                item.TenantId == _currentUserProvider.TenantId
                && !item.IsDeleted
                && (!query.AssetType.HasValue || item.AssetType == query.AssetType.Value)
                && (!query.Status.HasValue || item.Status == query.Status.Value)
                && (excludedStatuses.Count == 0 || !excludedStatuses.Contains(item.Status))
                && (!query.AvailableForLease.HasValue || item.IsAvailableForLease == query.AvailableForLease.Value)
                && (!query.AvailableForSale.HasValue || item.IsAvailableForSale == query.AvailableForSale.Value)
                && (!query.AvailableForSaleOrLease.HasValue
                    || (item.IsAvailableForSale || item.IsAvailableForLease) == query.AvailableForSaleOrLease.Value));

        if (normalizedSearch != null)
        {
            var search = $"%{normalizedSearch}%";
            assetsQuery = assetsQuery.Where(item =>
                (item.AssetCode != null && EF.Functions.Like(item.AssetCode, search))
                || (item.Name != null && EF.Functions.Like(item.Name, search))
                || (item.ProjectCode != null && EF.Functions.Like(item.ProjectCode, search))
                || (item.ProjectTitle != null && EF.Functions.Like(item.ProjectTitle, search))
                || (item.ProjectUnitCode != null && EF.Functions.Like(item.ProjectUnitCode, search))
                || (item.Purpose != null && EF.Functions.Like(item.Purpose, search))
                || (item.ZoningClassification != null && EF.Functions.Like(item.ZoningClassification, search))
                || (item.GisLayerReference != null && EF.Functions.Like(item.GisLayerReference, search))
                || (item.Location != null && EF.Functions.Like(item.Location, search)));
        }

        var assets = await assetsQuery
            .OrderBy(item => item.AssetCode)
            .ThenBy(item => item.Name)
            .Take(take)
            .ToListAsync();

        return assets.Select(MapToDto).ToList();
    }

    public async Task<EstateManagedAssetDto> PublishLandAcquisitionAsync(LandAcquisitionEstateHandoffDto handoff)
    {
        if (handoff.LandAcquisitionId == Guid.Empty)
        {
            throw new InvalidOperationException("Land acquisition is required.");
        }

        if (string.IsNullOrWhiteSpace(handoff.ProjectReference))
        {
            throw new InvalidOperationException("Project reference is required.");
        }

        var repository = _unitOfWork.Repository<EstateManagedAsset>();
        var existing = await repository.FirstOrDefaultAsync(item =>
            item.TenantId == _currentUserProvider.TenantId
            && !item.IsDeleted
            && item.LandAcquisitionId == handoff.LandAcquisitionId);

        var now = DateTime.UtcNow;
        var isNew = existing == null;
        var asset = existing ?? new EstateManagedAsset
        {
            TenantId = _currentUserProvider.TenantId,
            SourceType = EstateManagedAssetSourceType.LandAcquisition,
            LandAcquisitionId = handoff.LandAcquisitionId,
            CreatedBy = _currentUserProvider.Username,
            CreatedById = _currentUserProvider.UserId
        };

        var assetCode = FirstNonBlank(handoff.AssetCode, handoff.AssetNumber, handoff.ParcelIdentifier, handoff.ProjectReference);
        asset.AssetCode = assetCode;
        asset.Name = FirstNonBlank(handoff.Name, handoff.ParcelIdentifier, $"{handoff.ProjectReference} demarcated land");
        asset.Description = FirstNonBlank(handoff.Description, BuildLandAcquisitionDescription(handoff));
        asset.Location = TrimOrNull(handoff.Location);
        asset.Purpose = TrimOrNull(handoff.Purpose);
        asset.ZoningClassification = TrimOrNull(handoff.ZoningClassification);
        asset.PlanningComplianceStatus = TrimOrNull(handoff.PlanningComplianceStatus) ?? "Pending";
        asset.GisLayerReference = TrimOrNull(handoff.GisLayerReference);
        asset.BoundaryVerified = handoff.BoundaryVerified;
        asset.BoundaryCoordinates = TrimOrNull(handoff.BoundaryCoordinates);
        asset.SurveyPlanNumber = TrimOrNull(handoff.SurveyPlanNumber);
        asset.MapSheetNumber = TrimOrNull(handoff.MapSheetNumber);
        asset.AssetType = EstateManagedAssetType.Land;
        asset.Status = EstateManagedAssetStatus.LandBank;
        asset.ProjectCode = handoff.ProjectReference.Trim();
        asset.ProjectTitle = FirstNonBlank(handoff.ParcelIdentifier, handoff.Name, handoff.ProjectReference);
        asset.AreaSquareMeters = handoff.AreaSquareMeters;
        asset.ValuationAmount = handoff.ValuationAmount;
        asset.Currency = string.IsNullOrWhiteSpace(handoff.Currency) ? "GHS" : handoff.Currency.Trim().ToUpperInvariant();
        asset.IsAvailableForLease = false;
        asset.IsAvailableForSale = false;
        asset.IsPublishedFromProject = false;
        asset.IsReadyForProjectManagement = true;
        asset.Notes = TrimOrNull(handoff.Notes);
        asset.UpdatedAt = now;
        asset.UpdatedBy = _currentUserProvider.Username;
        asset.LastModifiedById = _currentUserProvider.UserId;

        if (isNew)
        {
            await repository.AddAsync(asset);
        }

        await _unitOfWork.SaveChangesAsync();
        return MapToDto(asset);
    }

    public async Task<EstateManagedAssetDto> PublishProjectUnitAsync(ProjectUnitEstateHandoffDto handoff)
    {
        if (handoff.ProjectId == Guid.Empty)
        {
            throw new InvalidOperationException("Project is required.");
        }

        if (handoff.ProjectUnitId == Guid.Empty)
        {
            throw new InvalidOperationException("Project unit is required.");
        }

        if (string.IsNullOrWhiteSpace(handoff.ProjectUnitName))
        {
            throw new InvalidOperationException("Project unit name is required.");
        }

        var repository = _unitOfWork.Repository<EstateManagedAsset>();
        var existing = await repository.FirstOrDefaultAsync(item =>
            item.TenantId == _currentUserProvider.TenantId
            && !item.IsDeleted
            && item.ProjectUnitId == handoff.ProjectUnitId);

        var now = DateTime.UtcNow;
        var isNew = existing == null;
        var asset = existing ?? new EstateManagedAsset
        {
            TenantId = _currentUserProvider.TenantId,
            SourceType = EstateManagedAssetSourceType.ProjectUnit,
            ProjectUnitId = handoff.ProjectUnitId,
            CreatedBy = _currentUserProvider.Username,
            CreatedById = _currentUserProvider.UserId,
            PublishedFromProjectAt = now
        };

        asset.AssetCode = BuildAssetCode(handoff);
        asset.Name = handoff.ProjectUnitName.Trim();
        asset.Description = BuildDescription(handoff);
        asset.Location = TrimOrNull(handoff.Location);
        asset.BlockName = TrimOrNull(handoff.BlockName);
        asset.FloorLabel = TrimOrNull(handoff.FloorLabel);
        asset.AssetType = ResolveAssetType(handoff.UnitType);
        asset.Status = ResolveStatus(handoff);
        asset.ProjectId = handoff.ProjectId;
        asset.ProjectCode = TrimOrNull(handoff.ProjectCode);
        asset.ProjectTitle = TrimOrNull(handoff.ProjectTitle);
        asset.ProjectUnitCode = TrimOrNull(handoff.ProjectUnitCode);
        asset.UnitType = TrimOrNull(handoff.UnitType);
        asset.AreaSquareMeters = handoff.AreaSquareMeters;
        asset.ValuationAmount = handoff.ValuationAmount;
        asset.Currency = string.IsNullOrWhiteSpace(handoff.Currency) ? "GHS" : handoff.Currency.Trim().ToUpperInvariant();
        asset.IsAvailableForLease = handoff.IsAvailableForLease;
        asset.IsAvailableForSale = handoff.IsAvailableForSale;
        asset.IsPublishedFromProject = true;
        asset.PublishedFromProjectAt ??= now;
        asset.Notes = TrimOrNull(handoff.Notes);
        asset.UpdatedAt = now;
        asset.UpdatedBy = _currentUserProvider.Username;
        asset.LastModifiedById = _currentUserProvider.UserId;

        if (isNew)
        {
            await repository.AddAsync(asset);
        }

        await _unitOfWork.SaveChangesAsync();
        return MapToDto(asset);
    }

    public async Task WithdrawProjectUnitAsync(Guid projectUnitId)
    {
        if (projectUnitId == Guid.Empty)
        {
            return;
        }

        var repository = _unitOfWork.Repository<EstateManagedAsset>();
        var asset = await repository.FirstOrDefaultAsync(item =>
            item.TenantId == _currentUserProvider.TenantId
            && !item.IsDeleted
            && item.SourceType == EstateManagedAssetSourceType.ProjectUnit
            && item.ProjectUnitId == projectUnitId);

        if (asset == null)
        {
            return;
        }

        asset.Status = EstateManagedAssetStatus.Retired;
        asset.IsAvailableForLease = false;
        asset.IsAvailableForSale = false;
        asset.IsPublishedToExternalPortal = false;
        asset.ExternalListingStatus = "Withdrawn";
        asset.ExternalListingType = "None";
        asset.ExternalPublishedAt = null;
        asset.UpdatedAt = DateTime.UtcNow;
        asset.UpdatedBy = _currentUserProvider.Username;
        asset.LastModifiedById = _currentUserProvider.UserId;

        await repository.UpdateAsync(asset);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<EstateManagedAssetDto> CreateManualExistingLandAsync(CreateManualExistingLandDto request)
    {
        var required = new[] { request.AssetCode, request.Name, request.Location, request.Purpose, request.ZoningClassification,
            request.PlanningComplianceStatus, request.CadastreDescription, request.Region, request.District, request.Town,
            request.AreaUnit, request.SurveyorName, request.SurveyPlanNumber, request.MapSheetNumber, request.BoundaryCoordinates };
        if (required.Any(string.IsNullOrWhiteSpace) || request.AreaValue <= 0 || request.AreaSquareMeters <= 0 ||
            request.BeaconCount < 3 || request.OwnershipHistory.Count == 0)
        {
            throw new InvalidOperationException("Complete all existing-land, cadastral, beacon, and ownership fields before saving.");
        }

        if (request.OwnershipHistory.Any(owner =>
            string.IsNullOrWhiteSpace(owner.OwnerName) || string.IsNullOrWhiteSpace(owner.OwnershipType) ||
            string.IsNullOrWhiteSpace(owner.InterestHeld) || string.IsNullOrWhiteSpace(owner.IdentificationType) ||
            string.IsNullOrWhiteSpace(owner.IdentificationNumber) || string.IsNullOrWhiteSpace(owner.ContactNumber) ||
            string.IsNullOrWhiteSpace(owner.Address) || !owner.OwnershipStartDate.HasValue || owner.OwnershipPercentage <= 0))
        {
            throw new InvalidOperationException("Complete every required current and previous owner field before saving.");
        }

        if (request.IsReadyForProjectManagement && !request.BoundaryVerified)
        {
            throw new InvalidOperationException("Verify the cadastral boundary before making the land ready for project management.");
        }

        var repository = _unitOfWork.Repository<EstateManagedAsset>();
        var duplicate = await repository.FirstOrDefaultAsync(item => item.TenantId == _currentUserProvider.TenantId &&
            !item.IsDeleted && item.AssetCode == request.AssetCode.Trim());
        if (duplicate != null) throw new InvalidOperationException("An estate asset with this asset code already exists.");

        var asset = new EstateManagedAsset
        {
            TenantId = _currentUserProvider.TenantId,
            CreatedBy = _currentUserProvider.Username,
            CreatedById = _currentUserProvider.UserId,
            AssetCode = request.AssetCode.Trim(),
            Name = request.Name.Trim(),
            Description = request.Description.Trim(),
            Location = request.Location.Trim(),
            Purpose = request.Purpose.Trim(),
            ZoningClassification = request.ZoningClassification.Trim(),
            PlanningComplianceStatus = request.PlanningComplianceStatus.Trim(),
            CadastreDescription = request.CadastreDescription.Trim(),
            Region = request.Region.Trim(),
            District = request.District.Trim(),
            Town = request.Town.Trim(),
            AreaValue = request.AreaValue,
            AreaUnit = request.AreaUnit.Trim(),
            AreaSquareMeters = request.AreaSquareMeters,
            SurveyorName = request.SurveyorName.Trim(),
            SurveyDate = request.SurveyDate,
            SurveyPlanNumber = request.SurveyPlanNumber.Trim(),
            MapSheetNumber = request.MapSheetNumber.Trim(),
            BeaconCount = request.BeaconCount,
            BoundaryCoordinates = request.BoundaryCoordinates.Trim(),
            BoundaryVerified = request.BoundaryVerified,
            OwnershipHistoryJson = JsonSerializer.Serialize(request.OwnershipHistory),
            ValuationAmount = request.ValuationAmount,
            Currency = string.IsNullOrWhiteSpace(request.Currency) ? "GHS" : request.Currency.Trim().ToUpperInvariant(),
            Notes = request.Notes.Trim(),
            AssetType = EstateManagedAssetType.Land,
            Status = EstateManagedAssetStatus.LandBank,
            SourceType = EstateManagedAssetSourceType.Manual,
            IsReadyForProjectManagement = request.IsReadyForProjectManagement,
            IsAvailableForLease = false,
            IsAvailableForSale = false
        };
        await repository.AddAsync(asset);
        await _unitOfWork.SaveChangesAsync();
        return MapToDto(asset);
    }

    public async Task<EstateManagedAssetDto> UpdateLandDemarcationAsync(Guid assetId, UpdateEstateManagedLandDemarcationDto request)
    {
        var required = new[] { request.CadastreDescription, request.Region, request.District, request.Town,
            request.AreaUnit, request.SurveyorName, request.SurveyPlanNumber, request.MapSheetNumber, request.BoundaryCoordinates };
        if (required.Any(string.IsNullOrWhiteSpace) || request.AreaValue <= 0 || request.BeaconCount < 3)
        {
            throw new InvalidOperationException("Complete the cadastral, beacon, and survey fields before saving demarcation.");
        }

        if (request.IsReadyForProjectManagement && !request.BoundaryVerified)
        {
            throw new InvalidOperationException("Verify the cadastral boundary before making the land ready for project management.");
        }

        var repository = _unitOfWork.Repository<EstateManagedAsset>();
        var asset = await repository.FirstOrDefaultAsync(item => item.Id == assetId &&
            item.TenantId == _currentUserProvider.TenantId && !item.IsDeleted && item.AssetType == EstateManagedAssetType.Land);
        if (asset == null) throw new InvalidOperationException("Land asset was not found.");

        asset.CadastreDescription = request.CadastreDescription.Trim();
        asset.Region = request.Region.Trim();
        asset.District = request.District.Trim();
        asset.Town = request.Town.Trim();
        asset.AreaValue = request.AreaValue;
        asset.AreaUnit = request.AreaUnit.Trim();
        asset.AreaSquareMeters = request.AreaSquareMeters > 0 ? request.AreaSquareMeters : asset.AreaSquareMeters;
        asset.SurveyorName = request.SurveyorName.Trim();
        asset.SurveyDate = request.SurveyDate ?? asset.SurveyDate;
        asset.SurveyPlanNumber = request.SurveyPlanNumber.Trim();
        asset.MapSheetNumber = request.MapSheetNumber.Trim();
        asset.BeaconCount = request.BeaconCount;
        asset.BoundaryCoordinates = request.BoundaryCoordinates.Trim();
        asset.BoundaryVerified = request.BoundaryVerified;
        asset.IsReadyForProjectManagement = request.IsReadyForProjectManagement;
        asset.UpdatedAt = DateTime.UtcNow;
        asset.UpdatedBy = _currentUserProvider.Username;
        asset.LastModifiedById = _currentUserProvider.UserId;

        await repository.UpdateAsync(asset);
        await _unitOfWork.SaveChangesAsync();
        return MapToDto(asset);
    }

    public async Task<EstateManagedAssetDto> MarkReadyForProjectManagementAsync(Guid assetId)
    {
        var repository = _unitOfWork.Repository<EstateManagedAsset>();
        var asset = await repository.FirstOrDefaultAsync(item => item.Id == assetId &&
            item.TenantId == _currentUserProvider.TenantId && !item.IsDeleted && item.AssetType == EstateManagedAssetType.Land);
        if (asset == null) throw new InvalidOperationException("Land asset was not found.");
        if (!asset.BoundaryVerified || string.IsNullOrWhiteSpace(asset.BoundaryCoordinates))
            throw new InvalidOperationException("Verify and record the cadastral boundary before project handoff.");
        asset.IsReadyForProjectManagement = true;
        asset.UpdatedAt = DateTime.UtcNow;
        asset.UpdatedBy = _currentUserProvider.Username;
        asset.LastModifiedById = _currentUserProvider.UserId;
        await repository.UpdateAsync(asset);
        await _unitOfWork.SaveChangesAsync();
        return MapToDto(asset);
    }

    public async Task<EstateManagedAssetDto> UpdateExternalListingAsync(Guid assetId, UpdateEstateManagedAssetListingDto request)
    {
        var repository = _unitOfWork.Repository<EstateManagedAsset>();
        var asset = await repository.FirstOrDefaultAsync(item => item.Id == assetId &&
            item.TenantId == _currentUserProvider.TenantId && !item.IsDeleted);
        if (asset == null) throw new InvalidOperationException("Estate asset was not found.");

        var listingType = NormalizeListingType(request.ExternalListingType);
        if (request.IsPublishedToExternalPortal && listingType == "None")
        {
            throw new InvalidOperationException("Select Sale, Rent, or Sale and Rent before publishing to the external portal.");
        }

        asset.IsPublishedToExternalPortal = request.IsPublishedToExternalPortal;
        asset.ExternalListingType = listingType;
        asset.ExternalListingStatus = request.IsPublishedToExternalPortal ? NormalizeListingStatus(request.ExternalListingStatus) : "Draft";
        asset.ExternalListingPrice = request.ExternalListingPrice > 0 ? request.ExternalListingPrice : null;
        asset.ExternalListingCurrency = string.IsNullOrWhiteSpace(request.ExternalListingCurrency)
            ? "GHS"
            : request.ExternalListingCurrency.Trim().ToUpperInvariant();
        asset.ExternalListingNotes = TrimOrNull(request.ExternalListingNotes);
        asset.ExternalPublishedAt = request.IsPublishedToExternalPortal
            ? asset.ExternalPublishedAt ?? DateTime.UtcNow
            : null;
        asset.IsAvailableForSale = listingType is "Sale" or "SaleAndRent";
        asset.IsAvailableForLease = listingType is "Rent" or "SaleAndRent";
        asset.UpdatedAt = DateTime.UtcNow;
        asset.UpdatedBy = _currentUserProvider.Username;
        asset.LastModifiedById = _currentUserProvider.UserId;

        await repository.UpdateAsync(asset);
        await _unitOfWork.SaveChangesAsync();
        return MapToDto(asset);
    }

    public async Task<EstateManagedAssetDocumentDto> RegisterDocumentAsync(Guid assetId, RegisterEstateManagedAssetDocumentDto document)
    {
        var asset = await _unitOfWork.Repository<EstateManagedAsset>().FirstOrDefaultAsync(item => item.Id == assetId &&
            item.TenantId == _currentUserProvider.TenantId && !item.IsDeleted);
        if (asset == null) throw new InvalidOperationException("Estate asset was not found.");
        var entity = new EstateManagedAssetDocument
        {
            TenantId = _currentUserProvider.TenantId,
            CreatedBy = _currentUserProvider.Username,
            CreatedById = _currentUserProvider.UserId,
            EstateManagedAssetId = assetId,
            FileName = document.FileName.Trim(),
            FilePath = document.FilePath.Trim(),
            DocumentType = string.IsNullOrWhiteSpace(document.DocumentType) ? "Other" : document.DocumentType.Trim(),
            DocumentName = TrimOrNull(document.DocumentName),
            ContentType = TrimOrNull(document.ContentType),
            FileSize = document.FileSize,
            IsListingImage = document.IsListingImage,
            IsPrimaryListingImage = document.IsPrimaryListingImage
        };

        if (entity.IsPrimaryListingImage)
        {
            var existingImages = await _unitOfWork.Repository<EstateManagedAssetDocument>().FindAsync(item =>
                item.EstateManagedAssetId == assetId && item.TenantId == _currentUserProvider.TenantId && !item.IsDeleted);
            foreach (var image in existingImages.Where(item => item.IsPrimaryListingImage))
            {
                image.IsPrimaryListingImage = false;
                image.UpdatedAt = DateTime.UtcNow;
                image.UpdatedBy = _currentUserProvider.Username;
                image.LastModifiedById = _currentUserProvider.UserId;
                await _unitOfWork.Repository<EstateManagedAssetDocument>().UpdateAsync(image);
            }
        }

        await _unitOfWork.Repository<EstateManagedAssetDocument>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return MapDocument(entity);
    }

    public async Task<EstateManagedAssetDocumentDto> SetPrimaryListingImageAsync(Guid assetId, Guid documentId)
    {
        var repository = _unitOfWork.Repository<EstateManagedAssetDocument>();
        var documents = (await repository.FindAsync(item =>
                item.EstateManagedAssetId == assetId && item.TenantId == _currentUserProvider.TenantId && !item.IsDeleted))
            .ToList();
        var selected = documents.FirstOrDefault(item => item.Id == documentId);
        if (selected == null) throw new InvalidOperationException("Listing image was not found.");
        if (!IsImage(selected)) throw new InvalidOperationException("Only image files can be used as listing images.");

        foreach (var document in documents)
        {
            document.IsListingImage = document.Id == documentId || document.IsListingImage;
            document.IsPrimaryListingImage = document.Id == documentId;
            document.UpdatedAt = DateTime.UtcNow;
            document.UpdatedBy = _currentUserProvider.Username;
            document.LastModifiedById = _currentUserProvider.UserId;
            await repository.UpdateAsync(document);
        }

        await _unitOfWork.SaveChangesAsync();
        return MapDocument(selected);
    }

    public async Task<IReadOnlyList<EstateManagedAssetDocumentDto>> GetDocumentsAsync(Guid assetId)
        => (await _unitOfWork.Repository<EstateManagedAssetDocument>().FindAsync(item =>
                item.EstateManagedAssetId == assetId && item.TenantId == _currentUserProvider.TenantId && !item.IsDeleted))
            .OrderByDescending(item => item.CreatedAt)
            .Select(MapDocument)
            .ToList();

    public async Task<(EstateManagedAssetDocumentDto Document, string FilePath)> GetDocumentAsync(Guid assetId, Guid documentId)
    {
        var document = await _unitOfWork.Repository<EstateManagedAssetDocument>().FirstOrDefaultAsync(item =>
            item.Id == documentId && item.EstateManagedAssetId == assetId && item.TenantId == _currentUserProvider.TenantId && !item.IsDeleted);
        if (document == null) throw new InvalidOperationException("Land document was not found.");
        return (MapDocument(document), document.FilePath);
    }

    private static EstateManagedAssetDocumentDto MapDocument(EstateManagedAssetDocument document) => new()
    {
        Id = document.Id,
        EstateManagedAssetId = document.EstateManagedAssetId,
        FileName = document.FileName,
        DocumentType = document.DocumentType,
        DocumentName = document.DocumentName,
        ContentType = document.ContentType,
        FileSize = document.FileSize,
        UploadedAt = document.CreatedAt,
        UploadedBy = document.CreatedBy,
        IsListingImage = document.IsListingImage,
        IsPrimaryListingImage = document.IsPrimaryListingImage,
        CentralDocumentRecordId = document.CentralDocumentRecordId,
        CentralDocumentReference = document.CentralDocumentReference,
        PublishedToCentralDmsAt = document.PublishedToCentralDmsAt
    };

    private static EstateManagedAssetType ResolveAssetType(string? unitType)
    {
        if (string.Equals(unitType, "Warehouse", StringComparison.OrdinalIgnoreCase)
            || string.Equals(unitType, "WholeBuilding", StringComparison.OrdinalIgnoreCase))
        {
            return EstateManagedAssetType.Facility;
        }

        return EstateManagedAssetType.Property;
    }

    private static EstateManagedAssetStatus ResolveStatus(ProjectUnitEstateHandoffDto handoff)
    {
        if (string.Equals(handoff.UnitStatus, "Leased", StringComparison.OrdinalIgnoreCase))
        {
            return EstateManagedAssetStatus.Leased;
        }

        if (string.Equals(handoff.UnitStatus, "Sold", StringComparison.OrdinalIgnoreCase))
        {
            return EstateManagedAssetStatus.Sold;
        }

        if (string.Equals(handoff.UnitStatus, "Occupied", StringComparison.OrdinalIgnoreCase))
        {
            return EstateManagedAssetStatus.Occupied;
        }

        if (string.Equals(handoff.UnitStatus, "Reserved", StringComparison.OrdinalIgnoreCase))
        {
            return EstateManagedAssetStatus.Reserved;
        }

        return EstateManagedAssetStatus.Available;
    }

    private static string BuildAssetCode(ProjectUnitEstateHandoffDto handoff)
    {
        var projectCode = string.IsNullOrWhiteSpace(handoff.ProjectCode) ? "PROJECT" : handoff.ProjectCode.Trim();
        var unitCode = string.IsNullOrWhiteSpace(handoff.ProjectUnitCode)
            ? handoff.ProjectUnitId.ToString("N")[..8].ToUpperInvariant()
            : handoff.ProjectUnitCode.Trim();

        return $"{projectCode}-{unitCode}";
    }

    private static string BuildDescription(ProjectUnitEstateHandoffDto handoff)
    {
        var area = handoff.AreaSquareMeters.HasValue ? $" ({handoff.AreaSquareMeters.Value:0.##} sqm)" : string.Empty;
        return $"{handoff.ProjectUnitName.Trim()} from {handoff.ProjectTitle.Trim()}{area}";
    }

    private static string BuildLandAcquisitionDescription(LandAcquisitionEstateHandoffDto handoff)
    {
        var parts = new List<string> { "Demarcated land ready for project management" };

        if (!string.IsNullOrWhiteSpace(handoff.SurveyPlanNumber))
        {
            parts.Add($"Survey plan: {handoff.SurveyPlanNumber.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(handoff.MapSheetNumber))
        {
            parts.Add($"Map sheet: {handoff.MapSheetNumber.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(handoff.Purpose))
        {
            parts.Add($"Purpose: {handoff.Purpose.Trim()}");
        }

        return string.Join(". ", parts);
    }

    private static EstateManagedAssetDto MapToDto(EstateManagedAsset asset) => new()
    {
        Id = asset.Id,
        AssetCode = asset.AssetCode,
        Name = asset.Name,
        Description = asset.Description,
        Location = asset.Location,
        Purpose = asset.Purpose,
        ZoningClassification = asset.ZoningClassification,
        PlanningComplianceStatus = asset.PlanningComplianceStatus,
        GisLayerReference = asset.GisLayerReference,
        BoundaryVerified = asset.BoundaryVerified,
        BoundaryCoordinates = asset.BoundaryCoordinates,
        SurveyPlanNumber = asset.SurveyPlanNumber,
        MapSheetNumber = asset.MapSheetNumber,
        CadastreDescription = asset.CadastreDescription,
        Region = asset.Region,
        District = asset.District,
        Town = asset.Town,
        AreaValue = asset.AreaValue,
        AreaUnit = asset.AreaUnit,
        SurveyorName = asset.SurveyorName,
        SurveyDate = asset.SurveyDate,
        BeaconCount = asset.BeaconCount,
        OwnershipHistory = string.IsNullOrWhiteSpace(asset.OwnershipHistoryJson)
            ? Array.Empty<ExistingLandOwnerDto>()
            : JsonSerializer.Deserialize<List<ExistingLandOwnerDto>>(asset.OwnershipHistoryJson) ?? [],
        IsReadyForProjectManagement = asset.IsReadyForProjectManagement,
        BlockName = asset.BlockName,
        FloorLabel = asset.FloorLabel,
        AssetType = asset.AssetType,
        Status = asset.Status,
        SourceType = asset.SourceType,
        LandAcquisitionId = asset.LandAcquisitionId,
        ProjectId = asset.ProjectId,
        ProjectUnitId = asset.ProjectUnitId,
        ProjectCode = asset.ProjectCode,
        ProjectTitle = asset.ProjectTitle,
        ProjectUnitCode = asset.ProjectUnitCode,
        UnitType = asset.UnitType,
        AreaSquareMeters = asset.AreaSquareMeters,
        ValuationAmount = asset.ValuationAmount,
        Currency = asset.Currency,
        IsAvailableForLease = asset.IsAvailableForLease,
        IsAvailableForSale = asset.IsAvailableForSale,
        IsPublishedFromProject = asset.IsPublishedFromProject,
        PublishedFromProjectAt = asset.PublishedFromProjectAt,
        IsPublishedToExternalPortal = asset.IsPublishedToExternalPortal,
        ExternalListingType = asset.ExternalListingType,
        ExternalListingStatus = asset.ExternalListingStatus,
        ExternalListingPrice = asset.ExternalListingPrice,
        ExternalListingCurrency = asset.ExternalListingCurrency,
        ExternalListingNotes = asset.ExternalListingNotes,
        ExternalPublishedAt = asset.ExternalPublishedAt,
        PrimaryListingImageDocumentId = asset.Documents?
            .Where(document => !document.IsDeleted && document.IsListingImage)
            .OrderByDescending(document => document.IsPrimaryListingImage)
            .ThenByDescending(document => document.CreatedAt)
            .Select(document => (Guid?)document.Id)
            .FirstOrDefault(),
        Notes = asset.Notes
    };

    private static string? TrimOrNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string FirstNonBlank(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? "LAND-ASSET";

    private static bool IsImage(EstateManagedAssetDocument document)
        => document.ContentType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true
            || document.FileName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
            || document.FileName.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
            || document.FileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
            || document.FileName.EndsWith(".webp", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeListingType(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "None" : value.Trim();
        return normalized.ToLowerInvariant() switch
        {
            "sale" => "Sale",
            "rent" or "lease" => "Rent",
            "saleandrent" or "sale and rent" or "sale/rent" or "both" => "SaleAndRent",
            _ => "None"
        };
    }

    private static string NormalizeListingStatus(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "Published" : value.Trim();
        return normalized.ToLowerInvariant() switch
        {
            "draft" => "Draft",
            "published" or "active" => "Published",
            "paused" => "Paused",
            _ => "Published"
        };
    }
}
