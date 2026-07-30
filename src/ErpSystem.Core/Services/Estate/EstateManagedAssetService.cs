using ErpSystem.Core.DTOs.Estate;
using ErpSystem.Core.Entities.Estate;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Estate;
using Microsoft.EntityFrameworkCore;
using System.Data;
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
            .AsNoTracking()
            .OrderBy(item => item.AssetCode)
            .ThenBy(item => item.Name)
            .Take(take)
            .ToListAsync();

        await EnrichLandAcquisitionAssetsForReadAsync(assets);

        var mappedAssets = assets.Select(MapToDto).ToList();
        if (assets.Count == 0)
        {
            return mappedAssets;
        }

        var assetIds = assets.Select(item => item.Id).ToList();
        var demarcationCounts = await _unitOfWork.Repository<EstateLandDemarcation>()
            .GetQueryable(item => item.TenantId == _currentUserProvider.TenantId
                && assetIds.Contains(item.EstateManagedAssetId)
                && !item.IsDeleted)
            .GroupBy(item => item.EstateManagedAssetId)
            .Select(group => new
            {
                AssetId = group.Key,
                Count = group.Count(),
                VerifiedCount = group.Count(item => item.BoundaryVerified)
            })
            .ToDictionaryAsync(item => item.AssetId);

        foreach (var mappedAsset in mappedAssets)
        {
            if (demarcationCounts.TryGetValue(mappedAsset.Id, out var count))
            {
                mappedAsset.DemarcationCount = count.Count;
                mappedAsset.VerifiedDemarcationCount = count.VerifiedCount;
            }
        }

        return mappedAssets;
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
        asset.GisProvider = string.IsNullOrWhiteSpace(asset.GisProvider) ? "GeoServer" : asset.GisProvider;
        var boundaryCoordinates = TrimOrNull(handoff.BoundaryCoordinates);
        asset.BoundaryCoordinates = boundaryCoordinates;
        asset.BoundaryVerified = asset.BoundaryVerified || boundaryCoordinates != null;
        asset.SurveyPlanNumber = TrimOrNull(handoff.SurveyPlanNumber);
        asset.MapSheetNumber = TrimOrNull(handoff.MapSheetNumber);
        asset.CadastreDescription = TrimOrNull(handoff.CadastreDescription);
        asset.Region = TrimOrNull(handoff.Region);
        asset.District = TrimOrNull(handoff.District);
        asset.Town = TrimOrNull(handoff.Town);
        asset.AssetType = EstateManagedAssetType.Land;
        asset.Status = EstateManagedAssetStatus.LandBank;
        asset.ProjectCode = handoff.ProjectReference.Trim();
        asset.ProjectTitle = FirstNonBlank(handoff.ParcelIdentifier, handoff.Name, handoff.ProjectReference);
        asset.AreaSquareMeters = handoff.AreaSquareMeters;
        asset.AreaValue = handoff.AreaValue;
        asset.AreaUnit = TrimOrNull(handoff.AreaUnit);
        asset.SurveyorName = TrimOrNull(handoff.SurveyorName);
        asset.SurveyDate = handoff.SurveyDate;
        asset.BeaconCount = handoff.BeaconCount;
        // Preserve the verified acquisition ownership chain for later Estate searches and audits.
        asset.OwnershipHistoryJson = handoff.OwnershipHistory.Count == 0
            ? asset.OwnershipHistoryJson
            : JsonSerializer.Serialize(handoff.OwnershipHistory);
        asset.ValuationAmount = handoff.ValuationAmount;
        asset.Currency = string.IsNullOrWhiteSpace(handoff.Currency) ? "GHS" : handoff.Currency.Trim().ToUpperInvariant();
        asset.IsAvailableForLease = false;
        asset.IsAvailableForSale = false;
        asset.IsPublishedFromProject = false;
        // Acquisition publishes into Estate Land Bank; Land Management marks project readiness after demarcation.
        asset.IsReadyForProjectManagement = false;
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
        var required = new[] { request.Name, request.Location, request.Purpose, request.ZoningClassification,
            request.PlanningComplianceStatus, request.GisLayerReference, request.CadastreDescription, request.Region, request.District, request.Town,
            request.AreaUnit, request.SurveyorName, request.SurveyPlanNumber, request.MapSheetNumber, request.BoundaryCoordinates };
        if (required.Any(string.IsNullOrWhiteSpace) || request.AreaValue <= 0 || request.ValuationAmount <= 0 ||
            request.SurveyDate == default || request.BeaconCount < 3 || request.OwnershipHistory.Count == 0)
        {
            throw new InvalidOperationException("Complete all existing-land, cadastral, beacon, and ownership fields before saving.");
        }

        var currentOwnerCount = request.OwnershipHistory.Count(owner => owner.IsCurrentOwner);
        if (request.OwnershipHistory.Any(owner =>
            string.IsNullOrWhiteSpace(owner.OwnerName) || string.IsNullOrWhiteSpace(owner.OwnershipType) ||
            string.IsNullOrWhiteSpace(owner.InterestHeld) || string.IsNullOrWhiteSpace(owner.IdentificationType) ||
            string.IsNullOrWhiteSpace(owner.IdentificationNumber) || string.IsNullOrWhiteSpace(owner.ContactNumber) ||
            string.IsNullOrWhiteSpace(owner.Address) || !owner.OwnershipStartDate.HasValue ||
            (!owner.IsCurrentOwner && !owner.OwnershipEndDate.HasValue) || owner.OwnershipPercentage <= 0) ||
            currentOwnerCount != 1)
        {
            throw new InvalidOperationException("Complete every owner field and identify exactly one current owner before saving.");
        }

        var repository = _unitOfWork.Repository<EstateManagedAsset>();
        var assetId = Guid.NewGuid();
        var areaSquareMeters = ConvertAreaToSquareMeters(request.AreaValue, request.AreaUnit);
        var asset = new EstateManagedAsset
        {
            Id = assetId,
            TenantId = _currentUserProvider.TenantId,
            CreatedBy = _currentUserProvider.Username,
            CreatedById = _currentUserProvider.UserId,
            // Manual Land Bank registrations receive an immutable system identifier.
            AssetCode = $"LAND-{DateTime.UtcNow:yyyy}-{assetId:N}"[..18].ToUpperInvariant(),
            Name = request.Name.Trim(),
            Description = request.Description.Trim(),
            Location = request.Location.Trim(),
            Purpose = request.Purpose.Trim(),
            ZoningClassification = request.ZoningClassification.Trim(),
            PlanningComplianceStatus = request.PlanningComplianceStatus.Trim(),
            GisLayerReference = request.GisLayerReference.Trim(),
            GisProvider = "GeoServer",
            CadastreDescription = request.CadastreDescription.Trim(),
            Region = request.Region.Trim(),
            District = request.District.Trim(),
            Town = request.Town.Trim(),
            AreaValue = request.AreaValue,
            AreaUnit = request.AreaUnit.Trim(),
            AreaSquareMeters = areaSquareMeters,
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
            IsReadyForProjectManagement = false,
            IsAvailableForLease = false,
            IsAvailableForSale = false
        };
        await repository.AddAsync(asset);
        await _unitOfWork.SaveChangesAsync();
        return MapToDto(asset);
    }

    public async Task<IReadOnlyList<EstateLandDemarcationDto>> GetLandDemarcationsAsync(Guid assetId)
    {
        var asset = await RequireLandAssetAsync(assetId);
        var demarcations = await _unitOfWork.Repository<EstateLandDemarcation>()
            .GetQueryable(item => item.EstateManagedAssetId == assetId
                && item.TenantId == _currentUserProvider.TenantId
                && !item.IsDeleted)
            .OrderBy(item => item.DemarcationNumber)
            .ToListAsync();
        var assignedLandReferences = await GetAssignedProjectLandReferencesAsync();

        return demarcations.Select(item =>
        {
            var dto = MapDemarcationToDto(item);
            dto.LandReference = EstateLandDemarcationReference.Build(asset.AssetCode, item.DemarcationNumber);
            dto.IsAssignedToProject = IsDemarcationAssignedToProject(
                asset,
                demarcations,
                item,
                assignedLandReferences);
            return dto;
        }).ToList();
    }

    public async Task<IReadOnlyList<ProjectReadyLandDemarcationDto>> GetProjectReadyLandDemarcationsAsync(
        Guid? projectId = null)
    {
        var tenantId = _currentUserProvider.TenantId;
        var profilesQuery = _unitOfWork.Repository<ProjectDevelopmentProfile>()
            .GetQueryable(item =>
                item.TenantId == tenantId
                && !item.IsDeleted
                && item.LandReference != null);
        var currentLandReference = projectId.HasValue
            ? await profilesQuery
                .Where(item => item.ProjectId == projectId.Value)
                .Select(item => item.LandReference)
                .FirstOrDefaultAsync()
            : null;
        var assignedLandReferenceValues = await profilesQuery
            .Where(item => !projectId.HasValue || item.ProjectId != projectId.Value)
            .Select(item => item.LandReference!.Trim())
            .ToListAsync();
        var assignedLandReferences = assignedLandReferenceValues
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var assets = await _unitOfWork.Repository<EstateManagedAsset>()
            .GetQueryable(item =>
                item.TenantId == tenantId
                && !item.IsDeleted
                && item.AssetType == EstateManagedAssetType.Land)
            .AsNoTracking()
            .ToListAsync();
        if (assets.Count == 0)
        {
            return [];
        }

        var assetIds = assets.Select(item => item.Id).ToList();
        var demarcations = await _unitOfWork.Repository<EstateLandDemarcation>()
            .GetQueryable(item =>
                item.TenantId == tenantId
                && !item.IsDeleted
                && assetIds.Contains(item.EstateManagedAssetId))
            .AsNoTracking()
            .ToListAsync();
        var demarcationsByAsset = demarcations
            .GroupBy(item => item.EstateManagedAssetId)
            .ToDictionary(group => group.Key, group => (IReadOnlyCollection<EstateLandDemarcation>)group.ToList());
        var currentReferenceSet = string.IsNullOrWhiteSpace(currentLandReference)
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>([currentLandReference.Trim()], StringComparer.OrdinalIgnoreCase);
        var result = new List<ProjectReadyLandDemarcationDto>();

        foreach (var asset in assets)
        {
            if (!demarcationsByAsset.TryGetValue(asset.Id, out var assetDemarcations))
            {
                continue;
            }

            foreach (var demarcation in assetDemarcations.Where(item => item.BoundaryVerified))
            {
                var isCurrentSelection = IsDemarcationAssignedToProject(
                    asset,
                    assetDemarcations,
                    demarcation,
                    currentReferenceSet);
                var isAvailable = asset.Status == EstateManagedAssetStatus.LandBank
                    && asset.IsReadyForProjectManagement
                    && !asset.IsPublishedToExternalPortal
                    && !IsDemarcationAssignedToProject(
                        asset,
                        assetDemarcations,
                        demarcation,
                        assignedLandReferences);
                if (!isCurrentSelection && !isAvailable)
                {
                    continue;
                }

                result.Add(new ProjectReadyLandDemarcationDto
                {
                    AssetId = asset.Id,
                    AssetCode = asset.AssetCode,
                    AssetName = asset.Name,
                    AssetLocation = asset.Location,
                    DemarcationId = demarcation.Id,
                    LandReference = EstateLandDemarcationReference.Build(
                        asset.AssetCode,
                        demarcation.DemarcationNumber),
                    DemarcationNumber = demarcation.DemarcationNumber,
                    Description = demarcation.Description,
                    AreaSquareFeet = demarcation.AreaSquareFeet,
                    IsCurrentProjectSelection = isCurrentSelection
                });
            }
        }

        return result
            .OrderBy(item => item.AssetCode)
            .ThenBy(item => item.DemarcationNumber)
            .ToList();
    }

    public Task<EstateLandDemarcationDto> CreateLandDemarcationAsync(
        Guid assetId,
        SaveEstateLandDemarcationDto request)
        => ExecuteSerializableMutationAsync(
            () => CreateLandDemarcationCoreAsync(assetId, request));

    private async Task<EstateLandDemarcationDto> CreateLandDemarcationCoreAsync(
        Guid assetId,
        SaveEstateLandDemarcationDto request)
    {
        var asset = await RequireLandAssetAsync(assetId);
        EnsureDemarcationsCanBeChanged(asset);
        var measurement = ValidateDemarcationRequest(asset, request);
        var repository = _unitOfWork.Repository<EstateLandDemarcation>();
        var existingBoundaries = await repository
            .GetQueryable(item => item.EstateManagedAssetId == assetId
                && item.TenantId == _currentUserProvider.TenantId
                && !item.IsDeleted)
            .Select(item => item.BoundaryCoordinates)
            .ToListAsync();
        EnsureDoesNotOverlap(request.BoundaryCoordinates, existingBoundaries);
        var lastDemarcationNumber = await repository
            .GetQueryable(item => item.EstateManagedAssetId == assetId
                && item.TenantId == _currentUserProvider.TenantId)
            .Select(item => (int?)item.DemarcationNumber)
            .MaxAsync() ?? 0;

        var demarcation = new EstateLandDemarcation
        {
            TenantId = _currentUserProvider.TenantId,
            EstateManagedAssetId = assetId,
            DemarcationNumber = lastDemarcationNumber + 1,
            Description = request.Description.Trim(),
            BeaconCount = measurement.BeaconCount,
            BoundaryCoordinates = request.BoundaryCoordinates.Trim(),
            AreaSquareFeet = measurement.AreaSquareFeet,
            BoundaryVerified = request.BoundaryVerified,
            CreatedBy = _currentUserProvider.Username,
            CreatedById = _currentUserProvider.UserId
        };

        await repository.AddAsync(demarcation);
        await ResetProjectReadinessAsync(asset);
        await _unitOfWork.SaveChangesAsync();
        return MapDemarcationToDto(demarcation);
    }

    public Task<EstateLandDemarcationDto> UpdateLandDemarcationAsync(
        Guid assetId,
        Guid demarcationId,
        SaveEstateLandDemarcationDto request)
        => ExecuteSerializableMutationAsync(
            () => UpdateLandDemarcationCoreAsync(assetId, demarcationId, request));

    private async Task<EstateLandDemarcationDto> UpdateLandDemarcationCoreAsync(
        Guid assetId,
        Guid demarcationId,
        SaveEstateLandDemarcationDto request)
    {
        var asset = await RequireLandAssetAsync(assetId);
        EnsureDemarcationsCanBeChanged(asset);
        var measurement = ValidateDemarcationRequest(asset, request);
        var repository = _unitOfWork.Repository<EstateLandDemarcation>();
        var demarcation = await repository.FirstOrDefaultAsync(item =>
            item.Id == demarcationId
            && item.EstateManagedAssetId == assetId
            && item.TenantId == _currentUserProvider.TenantId
            && !item.IsDeleted);
        if (demarcation == null)
        {
            throw new InvalidOperationException("Land demarcation was not found.");
        }

        var activeDemarcations = (await repository.FindAsync(item =>
                item.EstateManagedAssetId == assetId
                && item.TenantId == _currentUserProvider.TenantId
                && !item.IsDeleted))
            .ToList();
        var assignedLandReferences = await GetAssignedProjectLandReferencesAsync();
        if (IsDemarcationAssignedToProject(
                asset,
                activeDemarcations,
                demarcation,
                assignedLandReferences))
        {
            throw new InvalidOperationException(
                "Land demarcations assigned to a project cannot be edited. Reassign the project land first.");
        }

        var otherBoundaries = await repository
            .GetQueryable(item => item.EstateManagedAssetId == assetId
                && item.Id != demarcationId
                && item.TenantId == _currentUserProvider.TenantId
                && !item.IsDeleted)
            .Select(item => item.BoundaryCoordinates)
            .ToListAsync();
        EnsureDoesNotOverlap(request.BoundaryCoordinates, otherBoundaries);

        demarcation.Description = request.Description.Trim();
        demarcation.BeaconCount = measurement.BeaconCount;
        demarcation.BoundaryCoordinates = request.BoundaryCoordinates.Trim();
        demarcation.AreaSquareFeet = measurement.AreaSquareFeet;
        demarcation.BoundaryVerified = request.BoundaryVerified;
        demarcation.UpdatedAt = DateTime.UtcNow;
        demarcation.UpdatedBy = _currentUserProvider.Username;
        demarcation.LastModifiedById = _currentUserProvider.UserId;

        await repository.UpdateAsync(demarcation);
        await ResetProjectReadinessAsync(asset);
        await _unitOfWork.SaveChangesAsync();
        return MapDemarcationToDto(demarcation);
    }

    public async Task DeleteLandDemarcationAsync(Guid assetId, Guid demarcationId)
    {
        await ExecuteSerializableMutationAsync(async () =>
        {
            await DeleteLandDemarcationCoreAsync(assetId, demarcationId);
            return true;
        });
    }

    private async Task DeleteLandDemarcationCoreAsync(Guid assetId, Guid demarcationId)
    {
        var asset = await RequireLandAssetAsync(assetId);
        EnsureDemarcationsCanBeChanged(asset);
        var repository = _unitOfWork.Repository<EstateLandDemarcation>();
        var demarcation = await repository.FirstOrDefaultAsync(item =>
            item.Id == demarcationId
            && item.EstateManagedAssetId == assetId
            && item.TenantId == _currentUserProvider.TenantId
            && !item.IsDeleted);
        if (demarcation == null)
        {
            throw new InvalidOperationException("Land demarcation was not found.");
        }

        var activeDemarcations = (await repository.FindAsync(item =>
                item.EstateManagedAssetId == assetId
                && item.TenantId == _currentUserProvider.TenantId
                && !item.IsDeleted))
            .ToList();
        var assignedLandReferences = await GetAssignedProjectLandReferencesAsync();
        if (IsDemarcationAssignedToProject(
                asset,
                activeDemarcations,
                demarcation,
                assignedLandReferences))
        {
            throw new InvalidOperationException(
                "Land demarcations assigned to a project cannot be deleted. Reassign the project land first.");
        }

        demarcation.IsDeleted = true;
        demarcation.UpdatedAt = DateTime.UtcNow;
        demarcation.UpdatedBy = _currentUserProvider.Username;
        demarcation.LastModifiedById = _currentUserProvider.UserId;
        await repository.UpdateAsync(demarcation);
        await ResetProjectReadinessAsync(asset);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<EstateManagedAssetDto> MarkReadyForProjectManagementAsync(Guid assetId)
        => await ExecuteSerializableMutationAsync(
            () => MarkReadyForProjectManagementCoreAsync(assetId));

    private async Task<EstateManagedAssetDto> MarkReadyForProjectManagementCoreAsync(Guid assetId)
    {
        var repository = _unitOfWork.Repository<EstateManagedAsset>();
        var asset = await repository.FirstOrDefaultAsync(item => item.Id == assetId &&
            item.TenantId == _currentUserProvider.TenantId && !item.IsDeleted && item.AssetType == EstateManagedAssetType.Land);
        if (asset == null) throw new InvalidOperationException("Land asset was not found.");
        if (asset.IsPublishedToExternalPortal)
            throw new InvalidOperationException("Withdraw the active external land listing before marking it ready for project management.");
        if (!asset.BoundaryVerified || string.IsNullOrWhiteSpace(asset.BoundaryCoordinates))
            throw new InvalidOperationException("Verify and record the cadastral boundary before project handoff.");
        var demarcations = await _unitOfWork.Repository<EstateLandDemarcation>()
            .GetQueryable(item => item.EstateManagedAssetId == assetId
                && item.TenantId == _currentUserProvider.TenantId
                && !item.IsDeleted)
            .Select(item => new { item.BoundaryVerified, item.BoundaryCoordinates })
            .ToListAsync();
        if (demarcations.Count == 0)
            throw new InvalidOperationException("Add at least one demarcation within the main cadastral boundary.");
        if (demarcations.Any(item => !item.BoundaryVerified))
            throw new InvalidOperationException("Verify every demarcation before project handoff.");
        foreach (var demarcation in demarcations)
        {
            EstateBoundaryGeometry.ValidateContained(
                asset.BoundaryCoordinates,
                demarcation.BoundaryCoordinates);
        }
        for (var first = 0; first < demarcations.Count; first++)
        {
            for (var second = first + 1; second < demarcations.Count; second++)
            {
                if (EstateBoundaryGeometry.Overlaps(
                    demarcations[first].BoundaryCoordinates,
                    demarcations[second].BoundaryCoordinates))
                {
                    throw new InvalidOperationException(
                        "Resolve overlapping demarcations before project handoff.");
                }
            }
        }
        asset.IsReadyForProjectManagement = true;
        asset.UpdatedAt = DateTime.UtcNow;
        asset.UpdatedBy = _currentUserProvider.Username;
        asset.LastModifiedById = _currentUserProvider.UserId;
        await repository.UpdateAsync(asset);
        await _unitOfWork.SaveChangesAsync();
        return MapToDto(asset);
    }

    public Task<EstateManagedAssetDto> UpdateExternalListingAsync(
        Guid assetId,
        UpdateEstateManagedAssetListingDto request)
        => ExecuteSerializableMutationAsync(
            () => UpdateExternalListingCoreAsync(assetId, request));

    private async Task<EstateManagedAssetDto> UpdateExternalListingCoreAsync(
        Guid assetId,
        UpdateEstateManagedAssetListingDto request)
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

        if (request.IsPublishedToExternalPortal && asset.AssetType == EstateManagedAssetType.Land)
        {
            if (asset.Status != EstateManagedAssetStatus.LandBank || asset.ProjectId.HasValue)
            {
                throw new InvalidOperationException("Land assigned to a development project cannot be published for sale.");
            }
            if (!asset.BoundaryVerified)
            {
                throw new InvalidOperationException("Verify the main cadastral boundary before publishing land for sale.");
            }

            var demarcations = (await _unitOfWork.Repository<EstateLandDemarcation>().FindAsync(item =>
                    item.EstateManagedAssetId == assetId
                    && item.TenantId == _currentUserProvider.TenantId
                    && !item.IsDeleted))
                .ToList();
            if (!demarcations.Any() || demarcations.Any(item => !item.BoundaryVerified))
            {
                throw new InvalidOperationException("Add and verify every land demarcation before publishing land for sale.");
            }
            var assignedLandReferences = await GetAssignedProjectLandReferencesAsync();
            if (demarcations.Any(item => IsDemarcationAssignedToProject(
                    asset,
                    demarcations,
                    item,
                    assignedLandReferences)))
            {
                throw new InvalidOperationException(
                    "Land with a demarcated portion assigned to a development project cannot be published for sale.");
            }

            // Sale and project handoff are exclusive choices. Withdrawing the listing allows
            // Estate to mark the parcel project-ready again after review.
            asset.IsReadyForProjectManagement = false;
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

    private async Task<EstateManagedAsset> RequireLandAssetAsync(Guid assetId)
    {
        var asset = await _unitOfWork.Repository<EstateManagedAsset>()
            .FirstOrDefaultAsync(item => item.Id == assetId
                && item.TenantId == _currentUserProvider.TenantId
                && !item.IsDeleted
                && item.AssetType == EstateManagedAssetType.Land);
        return asset ?? throw new InvalidOperationException("Land asset was not found.");
    }

    private async Task EnrichLandAcquisitionAssetsForReadAsync(
        IReadOnlyCollection<EstateManagedAsset> assets)
    {
        var repairCandidates = assets
            .Where(asset =>
                asset.SourceType == EstateManagedAssetSourceType.LandAcquisition
                && asset.LandAcquisitionId.HasValue
                && (string.IsNullOrWhiteSpace(asset.BoundaryCoordinates)
                    || string.IsNullOrWhiteSpace(asset.SurveyorName)
                    || !asset.SurveyDate.HasValue
                    || string.IsNullOrWhiteSpace(asset.Region)
                    || string.IsNullOrWhiteSpace(asset.District)
                    || string.IsNullOrWhiteSpace(asset.Town)
                    || !asset.BeaconCount.HasValue))
            .ToList();
        if (repairCandidates.Count == 0)
        {
            return;
        }

        var acquisitionIds = repairCandidates
            .Select(asset => asset.LandAcquisitionId!.Value)
            .Distinct()
            .ToList();
        var acquisitions = await _unitOfWork.Repository<LandAcquisition>()
            .GetQueryable(item =>
                item.TenantId == _currentUserProvider.TenantId
                && !item.IsDeleted
                && acquisitionIds.Contains(item.Id))
            .Select(item => new
            {
                item.Id,
                item.WorkspaceDataJson
            })
            .ToDictionaryAsync(item => item.Id);
        var surveys = await _unitOfWork.Repository<CadastralSurvey>()
            .GetQueryable(item =>
                item.TenantId == _currentUserProvider.TenantId
                && !item.IsDeleted
                && acquisitionIds.Contains(item.LandAcquisitionId))
            .OrderByDescending(item => item.UpdatedAt ?? item.CreatedAt)
            .ToListAsync();
        var latestSurveyByAcquisition = surveys
            .GroupBy(item => item.LandAcquisitionId)
            .ToDictionary(group => group.Key, group => group.First());

        foreach (var asset in repairCandidates)
        {
            if (!acquisitions.TryGetValue(asset.LandAcquisitionId!.Value, out var acquisition))
            {
                continue;
            }

            var workspace = ReadCadastralWorkspace(acquisition.WorkspaceDataJson);
            latestSurveyByAcquisition.TryGetValue(asset.LandAcquisitionId.Value, out var survey);

            SetIfBlank(
                asset.BoundaryCoordinates,
                FirstNonBlankOrNull(
                    survey?.BoundaryCoordinates,
                    WorkspaceText(workspace, "boundaryCoordinates")),
                value => asset.BoundaryCoordinates = value);
            SetIfBlank(
                asset.SurveyorName,
                FirstNonBlankOrNull(
                    survey?.SurveyorName,
                    WorkspaceText(workspace, "surveyorName")),
                value => asset.SurveyorName = value);
            if (!asset.SurveyDate.HasValue)
            {
                var surveyDate = survey?.SurveyDate ?? WorkspaceDate(workspace, "surveyDate");
                if (surveyDate.HasValue)
                {
                    asset.SurveyDate = surveyDate;
                }
            }

            SetIfBlank(
                asset.Region,
                WorkspaceText(workspace, "regionId"),
                value => asset.Region = value);
            SetIfBlank(
                asset.District,
                WorkspaceText(workspace, "districtId"),
                value => asset.District = value);
            SetIfBlank(
                asset.Town,
                WorkspaceText(workspace, "townId"),
                value => asset.Town = value);
            if (!asset.BeaconCount.HasValue)
            {
                var beaconCount = ParsePositiveInt(survey?.BeaconCount)
                    ?? WorkspaceInt(workspace, "beaconCount");
                if (beaconCount.HasValue)
                {
                    asset.BeaconCount = beaconCount;
                }
            }
        }
    }

    private static bool SetIfBlank(
        string? currentValue,
        string? sourceValue,
        Action<string> apply)
    {
        var normalizedSource = TrimOrNull(sourceValue);
        if (!string.IsNullOrWhiteSpace(currentValue) || normalizedSource == null)
        {
            return false;
        }

        apply(normalizedSource);
        return true;
    }

    private static Dictionary<string, JsonElement> ReadCadastralWorkspace(string? workspaceDataJson)
    {
        if (string.IsNullOrWhiteSpace(workspaceDataJson))
        {
            return new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var snapshots = JsonSerializer.Deserialize<Dictionary<int, Dictionary<string, JsonElement>>>(
                workspaceDataJson);
            return snapshots != null
                && snapshots.TryGetValue((int)AcquisitionProcedure.CadastralSurvey, out var cadastral)
                    ? new Dictionary<string, JsonElement>(cadastral, StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static string? WorkspaceText(
        IReadOnlyDictionary<string, JsonElement> workspace,
        string key)
    {
        if (!workspace.TryGetValue(key, out var value)
            || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String
            ? TrimOrNull(value.GetString())
            : TrimOrNull(value.ToString());
    }

    private static DateTime? WorkspaceDate(
        IReadOnlyDictionary<string, JsonElement> workspace,
        string key)
    {
        var value = WorkspaceText(workspace, key);
        return DateTime.TryParse(value, out var parsed) ? parsed : null;
    }

    private static int? WorkspaceInt(
        IReadOnlyDictionary<string, JsonElement> workspace,
        string key) =>
        ParsePositiveInt(WorkspaceText(workspace, key));

    private static int? ParsePositiveInt(string? value) =>
        int.TryParse(value, out var parsed) && parsed >= 0 ? parsed : null;

    private static EstateBoundaryMeasurement ValidateDemarcationRequest(
        EstateManagedAsset asset,
        SaveEstateLandDemarcationDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Description))
        {
            throw new InvalidOperationException("Enter a description for this demarcation.");
        }

        if (request.Description.Trim().Length > 1000)
        {
            throw new InvalidOperationException("The demarcation description cannot exceed 1000 characters.");
        }

        if (!asset.BoundaryVerified || string.IsNullOrWhiteSpace(asset.BoundaryCoordinates))
        {
            throw new InvalidOperationException(
                "The main cadastral boundary must be recorded and verified before demarcation.");
        }

        var measurement = EstateBoundaryGeometry.ValidateContained(
            asset.BoundaryCoordinates,
            request.BoundaryCoordinates);
        if (request.BeaconCount != measurement.BeaconCount)
        {
            throw new InvalidOperationException("The beacon count does not match the demarcation coordinates.");
        }

        return measurement;
    }

    private static void EnsureDoesNotOverlap(
        string boundaryCoordinates,
        IEnumerable<string> existingBoundaries)
    {
        if (existingBoundaries.Any(existing => EstateBoundaryGeometry.Overlaps(existing, boundaryCoordinates)))
        {
            throw new InvalidOperationException(
                "The demarcation overlaps another parcel already defined under this cadastral boundary.");
        }
    }

    private async Task ResetProjectReadinessAsync(EstateManagedAsset asset)
    {
        // Any subdivision change requires Estate to confirm the complete set again before Project Management can pull it.
        asset.IsReadyForProjectManagement = false;
        asset.UpdatedAt = DateTime.UtcNow;
        asset.UpdatedBy = _currentUserProvider.Username;
        asset.LastModifiedById = _currentUserProvider.UserId;
        await _unitOfWork.Repository<EstateManagedAsset>().UpdateAsync(asset);
    }

    private static void EnsureDemarcationsCanBeChanged(EstateManagedAsset asset)
    {
        if (asset.IsPublishedToExternalPortal)
        {
            throw new InvalidOperationException(
                "Withdraw the active external land listing before changing its demarcations.");
        }
    }

    private async Task<HashSet<string>> GetAssignedProjectLandReferencesAsync()
        => (await _unitOfWork.Repository<ProjectDevelopmentProfile>()
                .FindAsync(item => item.TenantId == _currentUserProvider.TenantId
                    && !item.IsDeleted
                    && item.LandReference != null))
            .Select(item => item.LandReference!.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private async Task<T> ExecuteSerializableMutationAsync<T>(Func<Task<T>> action)
    {
        return await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            var transactionStarted = false;
            try
            {
                await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);
                transactionStarted = true;
                var result = await action();
                await _unitOfWork.CommitAsync();
                transactionStarted = false;
                return result;
            }
            catch
            {
                if (transactionStarted)
                {
                    await _unitOfWork.RollbackAsync();
                }

                throw;
            }
        });
    }

    private static bool IsDemarcationAssignedToProject(
        EstateManagedAsset asset,
        IReadOnlyCollection<EstateLandDemarcation> demarcations,
        EstateLandDemarcation demarcation,
        ISet<string> assignedLandReferences)
    {
        var landReference = EstateLandDemarcationReference.Build(
            asset.AssetCode,
            demarcation.DemarcationNumber);
        if (assignedLandReferences.Contains(landReference))
        {
            return true;
        }

        return demarcations.Count == 1
            && new[] { asset.AssetCode, asset.ProjectCode, asset.Name, asset.Id.ToString() }
                .Any(reference => !string.IsNullOrWhiteSpace(reference)
                    && assignedLandReferences.Contains(reference.Trim()));
    }

    private static EstateLandDemarcationDto MapDemarcationToDto(EstateLandDemarcation demarcation) => new()
    {
        Id = demarcation.Id,
        EstateManagedAssetId = demarcation.EstateManagedAssetId,
        DemarcationNumber = demarcation.DemarcationNumber,
        Description = demarcation.Description,
        BeaconCount = demarcation.BeaconCount,
        BoundaryCoordinates = demarcation.BoundaryCoordinates,
        AreaSquareFeet = demarcation.AreaSquareFeet,
        BoundaryVerified = demarcation.BoundaryVerified,
        CreatedAt = demarcation.CreatedAt,
        CreatedBy = demarcation.CreatedBy
    };

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
        GisProvider = asset.GisProvider,
        GisFeatureId = asset.GisFeatureId,
        GisSourceCrs = asset.GisSourceCrs,
        GisSyncStatus = asset.GisSyncStatus,
        GisLastSyncedAt = asset.GisLastSyncedAt,
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

    private static string? FirstNonBlankOrNull(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private static decimal ConvertAreaToSquareMeters(decimal value, string unit)
        => unit.Trim().ToLowerInvariant() switch
        {
            "sq ft" or "sqft" or "square feet" => value * 0.09290304m,
            "acres" or "acre" => value * 4046.8564224m,
            "hectares" or "hectare" or "ha" => value * 10000m,
            "sqm" or "sq m" or "square metres" or "square meters" => value,
            _ => throw new InvalidOperationException("Select a supported survey area unit.")
        };

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
