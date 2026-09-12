using ErpSystem.Core.DTOs.Estate;
using ErpSystem.Core.Entities.Estate;
using ErpSystem.Core.Entities.Procurement;
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
                    || (item.IsAvailableForSale || item.IsAvailableForLease) == query.AvailableForSaleOrLease.Value)
                && (query.PortalListingCandidates != true
                    || item.IsPublishedToExternalPortal
                    || item.ExternalListingType != "None"));

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
                || (item.Location != null && EF.Functions.Like(item.Location, search))
                || (item.LesseeName != null && EF.Functions.Like(item.LesseeName, search))
                || (item.PropertyFileReference != null && EF.Functions.Like(item.PropertyFileReference, search)));
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
        => await ExecuteSerializableMutationAsync(
            () => PublishLandAcquisitionCoreAsync(handoff));

    private async Task<EstateManagedAssetDto> PublishLandAcquisitionCoreAsync(
        LandAcquisitionEstateHandoffDto handoff)
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
        if (existing != null)
        {
            await EnsureAcquisitionCanBeRepublishedAsync(existing);
        }

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
        // Demarcation references are derived from the managed-asset code. Once published,
        // keep that code immutable so persisted project references cannot be invalidated by
        // a later edit to the acquisition-stage code.
        if (isNew || string.IsNullOrWhiteSpace(asset.AssetCode))
        {
            asset.AssetCode = assetCode;
        }
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
        asset.BoundaryVerified =
            handoff.BoundaryVerified && boundaryCoordinates != null;
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
        asset.IsPublishedToExternalPortal = false;
        asset.ExternalListingType = "None";
        asset.ExternalListingStatus = "Draft";
        asset.ExternalListingPrice = null;
        asset.ExternalSalePrice = null;
        asset.ExternalMonthlyRent = null;
        asset.ExternalLeaseTermMonths = null;
        asset.ExternalListingNotes = null;
        asset.ExternalPublishedAt = null;
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

    private async Task EnsureAcquisitionCanBeRepublishedAsync(EstateManagedAsset asset)
    {
        // An asset deliberately moved back to Asset Creation may retain a stale
        // whole-asset portal flag from an earlier test/listing. Treat that flag
        // as invalid once the asset is no longer in Land Bank; the handoff above
        // resets the whole-asset listing state. A currently Land Bank-published
        // asset still requires an explicit withdrawal before republishing.
        if (asset.IsPublishedToExternalPortal
            && asset.Status == EstateManagedAssetStatus.LandBank)
        {
            throw new InvalidOperationException(
                "Withdraw the active external land listing before publishing the acquisition to the land bank again.");
        }

        if (asset.ProjectId.HasValue)
        {
            throw new InvalidOperationException(
                "Land assigned to a project cannot be published to the land bank again. Reassign the project land first.");
        }

        var demarcations = await _unitOfWork.Repository<EstateLandDemarcation>()
            .GetQueryable(item =>
                item.EstateManagedAssetId == asset.Id
                && item.TenantId == _currentUserProvider.TenantId
                && !item.IsDeleted)
            .ToListAsync();
        if (demarcations.Count == 0)
        {
            return;
        }

        var assignedLandReferences = await GetAssignedProjectLandReferencesAsync();
        if (demarcations.Any(item => IsDemarcationAssignedToProject(
                asset,
                demarcations,
                item,
                assignedLandReferences)))
        {
            throw new InvalidOperationException(
                "Land with a demarcation assigned to a project cannot be published to the land bank again. Reassign the project land first.");
        }
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
        var capitalizedCost = request.TotalCapitalizedCost.GetValueOrDefault();
        if (capitalizedCost <= 0m)
        {
            capitalizedCost =
                request.OwnerConsiderationCost.GetValueOrDefault() +
                request.ExternalSurveyorCost.GetValueOrDefault() +
                request.StampDutyCost.GetValueOrDefault() +
                request.OtherAcquisitionCost.GetValueOrDefault();
        }
        if (capitalizedCost <= 0m)
        {
            capitalizedCost = request.ValuationAmount;
        }

        var costBreakdown = new[]
            {
                request.OwnerConsiderationCost is > 0m ? $"Owner/vendor consideration: {request.Currency} {request.OwnerConsiderationCost.Value:N2}" : null,
                request.ExternalSurveyorCost is > 0m ? $"External surveyor cost: {request.Currency} {request.ExternalSurveyorCost.Value:N2}" : null,
                request.StampDutyCost is > 0m ? $"Stamp duty: {request.Currency} {request.StampDutyCost.Value:N2}" : null,
                request.OtherAcquisitionCost is > 0m ? $"Other acquisition cost: {request.Currency} {request.OtherAcquisitionCost.Value:N2}" : null,
                $"Total capitalized land cost: {request.Currency} {capitalizedCost:N2}"
            }
            .Where(item => !string.IsNullOrWhiteSpace(item));
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
            ValuationAmount = capitalizedCost,
            OwnerConsiderationCost = request.OwnerConsiderationCost,
            ExternalSurveyorCost = request.ExternalSurveyorCost,
            StampDutyCost = request.StampDutyCost,
            OtherAcquisitionCost = request.OtherAcquisitionCost,
            TotalCapitalizedCost = capitalizedCost,
            Currency = string.IsNullOrWhiteSpace(request.Currency) ? "GHS" : request.Currency.Trim().ToUpperInvariant(),
            Notes = string.Join(Environment.NewLine, new[]
            {
                request.Notes.Trim(),
                string.Join("; ", costBreakdown)
            }.Where(item => !string.IsNullOrWhiteSpace(item))),
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
            var dto = MapDemarcationToDto(item, asset.AssetCode);
            dto.IsAssignedToProject = IsDemarcationAssignedToProject(
                asset,
                demarcations,
                item,
                assignedLandReferences);
            dto.HasChildDemarcations =
                demarcations.Any(child => child.ParentDemarcationId == item.Id);
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
        var normalizedCurrentReference = string.IsNullOrWhiteSpace(currentLandReference)
            ? null
            : currentLandReference.Trim();
        var currentReferenceIsAssetId = Guid.TryParse(
            normalizedCurrentReference,
            out var currentAssetId);
        var currentReferenceIsDemarcation =
            EstateLandDemarcationReference.TryParse(
                normalizedCurrentReference,
                out var currentAssetCode,
                out var currentDemarcationNumber);

        // Keep the database query narrow: only ready assets and the current project's
        // selected asset are candidates, and only selector fields are projected.
        var candidates = await _unitOfWork.Repository<EstateLandDemarcation>()
            .GetQueryable(item =>
                item.TenantId == tenantId
                && !item.IsDeleted
                && item.EstateManagedAsset.TenantId == tenantId
                && !item.EstateManagedAsset.IsDeleted
                && item.EstateManagedAsset.AssetType == EstateManagedAssetType.Land
                // Only leaf demarcations can be assigned to a project. A parent parcel
                // overlaps its child parcels and must remain a grouping boundary.
                && !item.EstateManagedAsset.Demarcations.Any(child =>
                    !child.IsDeleted && child.ParentDemarcationId == item.Id)
                && ((item.EstateManagedAsset.Status == EstateManagedAssetStatus.LandBank
                        && (item.IsReadyForProjectManagement || item.EstateManagedAsset.IsReadyForProjectManagement)
                        && !item.IsPublishedToExternalPortal
                        && !item.EstateManagedAsset.IsPublishedToExternalPortal)
                    || (normalizedCurrentReference != null
                        && ((currentReferenceIsAssetId
                                && item.EstateManagedAssetId == currentAssetId)
                            || item.EstateManagedAsset.AssetCode == normalizedCurrentReference
                            || item.EstateManagedAsset.ProjectCode == normalizedCurrentReference
                            || item.EstateManagedAsset.Name == normalizedCurrentReference
                            || (currentReferenceIsDemarcation
                                && item.EstateManagedAsset.AssetCode == currentAssetCode
                                && item.DemarcationNumber == currentDemarcationNumber)))))
            .Select(item => new ReadyLandCandidate
            {
                AssetId = item.EstateManagedAssetId,
                AssetCode = item.EstateManagedAsset.AssetCode,
                AssetName = item.EstateManagedAsset.Name,
                AssetLocation = item.EstateManagedAsset.Location,
                AssetProjectCode = item.EstateManagedAsset.ProjectCode,
                AssetStatus = item.EstateManagedAsset.Status,
                AssetIsReadyForProjectManagement =
                    item.EstateManagedAsset.IsReadyForProjectManagement,
                AssetIsPublishedToExternalPortal =
                    item.EstateManagedAsset.IsPublishedToExternalPortal,
                DemarcationId = item.Id,
                DemarcationIsReadyForProjectManagement = item.IsReadyForProjectManagement,
                DemarcationIsPublishedToExternalPortal = item.IsPublishedToExternalPortal,
                DemarcationNumber = item.DemarcationNumber,
                Description = item.Description,
                AreaSquareFeet = item.AreaSquareFeet,
                BoundaryVerified = item.BoundaryVerified
            })
            .AsNoTracking()
            .ToListAsync();
        var candidatesByAsset = candidates
            .GroupBy(item => item.AssetId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyCollection<ReadyLandCandidate>)group.ToList());
        var currentReferenceSet = string.IsNullOrWhiteSpace(currentLandReference)
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>([currentLandReference.Trim()], StringComparer.OrdinalIgnoreCase);
        var result = new List<ProjectReadyLandDemarcationDto>();

        foreach (var assetCandidates in candidatesByAsset.Values)
        {
            foreach (var candidate in assetCandidates.Where(item => item.BoundaryVerified))
            {
                var isCurrentSelection = IsReadyLandCandidateAssignedToProject(
                    assetCandidates,
                    candidate,
                    currentReferenceSet);
                var isAvailable = candidate.AssetStatus == EstateManagedAssetStatus.LandBank
                    && (candidate.DemarcationIsReadyForProjectManagement || candidate.AssetIsReadyForProjectManagement)
                    && !candidate.AssetIsPublishedToExternalPortal
                    && !candidate.DemarcationIsPublishedToExternalPortal
                    && !IsReadyLandCandidateAssignedToProject(
                        assetCandidates,
                        candidate,
                        assignedLandReferences);
                if (!isCurrentSelection && !isAvailable)
                {
                    continue;
                }

                result.Add(new ProjectReadyLandDemarcationDto
                {
                    AssetId = candidate.AssetId,
                    AssetCode = candidate.AssetCode,
                    AssetName = candidate.AssetName,
                    AssetLocation = candidate.AssetLocation,
                    DemarcationId = candidate.DemarcationId,
                    LandReference = EstateLandDemarcationReference.Build(
                        candidate.AssetCode,
                        candidate.DemarcationNumber),
                    DemarcationNumber = candidate.DemarcationNumber,
                    Description = candidate.Description,
                    AreaSquareFeet = candidate.AreaSquareFeet,
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
        var parent = await ResolveParentDemarcationAsync(assetId, request.ParentDemarcationId);
        var measurement = ValidateDemarcationRequest(asset, request, parent);
        var repository = _unitOfWork.Repository<EstateLandDemarcation>();
        var activeDemarcations = (await repository.FindAsync(item =>
                item.EstateManagedAssetId == assetId
                && item.TenantId == _currentUserProvider.TenantId
                && !item.IsDeleted))
            .ToList();
        var existingBoundaries = activeDemarcations
            .Where(item => item.ParentDemarcationId == request.ParentDemarcationId)
            .Select(item => item.BoundaryCoordinates)
            .ToList();
        if (existingBoundaries.Count > 0)
        {
            var assignedLandReferences = await GetAssignedProjectLandReferencesAsync();
            if (HasLegacyWholeParcelAssignment(asset, assignedLandReferences))
            {
                throw new InvalidOperationException(
                    "Land assigned to a project by its whole-parcel reference cannot be subdivided. Reassign the project to the existing demarcation first.");
            }
        }
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
            ParentDemarcationId = request.ParentDemarcationId,
            DemarcationNumber = lastDemarcationNumber + 1,
            ParentLandAssetReference = asset.AssetCode,
            ParentFixedAssetReference = BuildParentFixedAssetReference(asset, parent),
            ChildFixedAssetReference = BuildChildFixedAssetReference(
                BuildParentFixedAssetReference(asset, parent),
                lastDemarcationNumber + 1),
            Description = request.Description.Trim(),
            BeaconCount = measurement.BeaconCount,
            BoundaryCoordinates = request.BoundaryCoordinates.Trim(),
            AreaSquareFeet = measurement.AreaSquareFeet,
            BoundaryVerified = request.BoundaryVerified,
            CreatedBy = _currentUserProvider.Username,
            CreatedById = _currentUserProvider.UserId
        };
        ApplyAutomaticDemarcationCosting(asset, activeDemarcations, demarcation, request.TargetSalePrice);
        var demarcationsForCosting = activeDemarcations.Append(demarcation).ToList();
        RebalanceDemarcationCostGroup(asset, demarcationsForCosting, request.ParentDemarcationId);

        await repository.AddAsync(demarcation);
        await ResetDemarcationReadinessAsync(asset, parent);
        await _unitOfWork.SaveChangesAsync();
        return MapDemarcationToDto(demarcation, asset.AssetCode);
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
        var parent = await ResolveParentDemarcationAsync(assetId, request.ParentDemarcationId);
        var measurement = ValidateDemarcationRequest(asset, request, parent);
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
        if (request.ParentDemarcationId == demarcation.Id)
        {
            throw new InvalidOperationException("A demarcation cannot be its own parent parcel.");
        }

        var activeDemarcations = (await repository.FindAsync(item =>
                item.EstateManagedAssetId == assetId
                && item.TenantId == _currentUserProvider.TenantId
                && !item.IsDeleted))
            .ToList();
        if (activeDemarcations.Any(item => item.ParentDemarcationId == demarcation.Id))
        {
            throw new InvalidOperationException(
                "Edit child demarcations before changing this parent parcel.");
        }
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
                && item.ParentDemarcationId == request.ParentDemarcationId
                && item.TenantId == _currentUserProvider.TenantId
                && !item.IsDeleted)
            .Select(item => item.BoundaryCoordinates)
            .ToListAsync();
        EnsureDoesNotOverlap(request.BoundaryCoordinates, otherBoundaries);

        var previousParentDemarcationId = demarcation.ParentDemarcationId;
        var boundaryChanged =
            !string.Equals(
                demarcation.BoundaryCoordinates?.Trim(),
                request.BoundaryCoordinates?.Trim(),
                StringComparison.Ordinal) ||
            demarcation.ParentDemarcationId != request.ParentDemarcationId;
        demarcation.ParentDemarcationId = request.ParentDemarcationId;
        demarcation.Description = request.Description.Trim();
        demarcation.BeaconCount = measurement.BeaconCount;
        demarcation.BoundaryCoordinates = request.BoundaryCoordinates.Trim();
        demarcation.AreaSquareFeet = measurement.AreaSquareFeet;
        demarcation.BoundaryVerified = request.BoundaryVerified;
        demarcation.ParentLandAssetReference = asset.AssetCode;
        demarcation.ParentFixedAssetReference = BuildParentFixedAssetReference(asset, parent);
        demarcation.ChildFixedAssetReference = BuildChildFixedAssetReference(
            demarcation.ParentFixedAssetReference!,
            demarcation.DemarcationNumber);
        if (boundaryChanged)
        {
            ApplyAutomaticDemarcationCosting(asset, activeDemarcations, demarcation, request.TargetSalePrice);
            demarcation.FixedAssetPostedAt = null;
        }
        else if (request.TargetSalePrice is > 0m)
        {
            demarcation.TargetSalePrice = request.TargetSalePrice;
        }
        RebalanceDemarcationCostGroup(asset, activeDemarcations, previousParentDemarcationId);
        if (previousParentDemarcationId != request.ParentDemarcationId)
        {
            RebalanceDemarcationCostGroup(asset, activeDemarcations, request.ParentDemarcationId);
        }
        demarcation.UpdatedAt = DateTime.UtcNow;
        demarcation.UpdatedBy = _currentUserProvider.Username;
        demarcation.LastModifiedById = _currentUserProvider.UserId;

        await repository.UpdateAsync(demarcation);
        await ResetDemarcationReadinessAsync(asset, parent);
        await _unitOfWork.SaveChangesAsync();
        return MapDemarcationToDto(demarcation, asset.AssetCode);
    }

    public Task<EstateLandDemarcationDto> UpdateLandDemarcationDispositionAsync(
        Guid assetId,
        Guid demarcationId,
        UpdateEstateLandDemarcationDispositionDto request)
        => ExecuteSerializableMutationAsync(
            () => UpdateLandDemarcationDispositionCoreAsync(assetId, demarcationId, request));

    private async Task<EstateLandDemarcationDto> UpdateLandDemarcationDispositionCoreAsync(
        Guid assetId,
        Guid demarcationId,
        UpdateEstateLandDemarcationDispositionDto request)
    {
        var asset = await RequireLandAssetAsync(assetId);
        var repository = _unitOfWork.Repository<EstateLandDemarcation>();
        var demarcations = (await repository.FindAsync(item =>
                item.EstateManagedAssetId == assetId
                && item.TenantId == _currentUserProvider.TenantId
                && !item.IsDeleted))
            .ToList();
        var demarcation = demarcations.FirstOrDefault(item => item.Id == demarcationId)
            ?? throw new InvalidOperationException("Land demarcation was not found.");
        var assignedLandReferences = await GetAssignedProjectLandReferencesAsync();
        if (request.IsPublishedToExternalPortal &&
            IsDemarcationAssignedToProject(asset, demarcations, demarcation, assignedLandReferences))
        {
            throw new InvalidOperationException("Demarcations assigned to a project cannot be sent to Portal Listings.");
        }

        if (request.IsReadyForProjectManagement && request.IsPublishedToExternalPortal)
        {
            throw new InvalidOperationException("Choose either Project Ready or Portal Listing for this demarcation, not both.");
        }

        if ((request.IsReadyForProjectManagement || request.IsPublishedToExternalPortal) &&
            (!demarcation.BoundaryVerified || string.IsNullOrWhiteSpace(demarcation.BoundaryCoordinates)))
        {
            throw new InvalidOperationException("Verify this demarcation before changing its project or portal status.");
        }

        if ((request.IsReadyForProjectManagement || request.IsPublishedToExternalPortal) &&
            demarcations.Any(item => item.ParentDemarcationId == demarcation.Id))
        {
            throw new InvalidOperationException(
                "This demarcation has child parcels. Mark the child parcels project-ready or send them to Portal Listings instead.");
        }
        if (request.IsReadyForProjectManagement || request.IsPublishedToExternalPortal)
        {
            EnsureDemarcationCostingReconcilesForOutbound(asset, demarcations, demarcation);
            if (demarcation.AllocatedCost is not > 0m)
            {
                throw new InvalidOperationException(
                    "Set the demarcation cost before making this parcel project-ready or sending it to Portal Listings.");
            }
        }

        var listingType = NormalizeListingType(request.ExternalListingType);
        var listingStatus = request.IsPublishedToExternalPortal
            ? NormalizeListingStatus(request.ExternalListingStatus)
            : "Draft";
        var publishToCustomerPortal = request.IsPublishedToExternalPortal
            && string.Equals(listingStatus, "Published", StringComparison.OrdinalIgnoreCase);
        if (request.IsPublishedToExternalPortal && listingType == "None")
        {
            throw new InvalidOperationException("Select Sale, Rent, or Sale and Rent before sending this demarcation to Portal Listings.");
        }

        var includesSale = listingType is "Sale" or "SaleAndRent";
        var includesRent = listingType is "Rent" or "SaleAndRent";
        var listingCurrency = string.IsNullOrWhiteSpace(request.ExternalListingCurrency)
            ? "GHS"
            : request.ExternalListingCurrency.Trim().ToUpperInvariant();
        var salePrice = includesSale
            ? request.ExternalSalePrice ?? request.ExternalListingPrice
            : null;
        salePrice ??= includesSale ? demarcation.TargetSalePrice : null;
        var monthlyRent = includesRent
            ? request.ExternalMonthlyRent ?? (!includesSale ? request.ExternalListingPrice : null)
            : null;
        if (publishToCustomerPortal && includesSale && salePrice is not > 0m)
        {
            throw new InvalidOperationException("Enter the sale price before listing this demarcation.");
        }
        if (includesSale && salePrice.HasValue && demarcation.TargetSalePrice is > 0m
            && salePrice.Value < demarcation.TargetSalePrice.Value)
        {
            throw new InvalidOperationException(
                $"The portal sale price cannot be below the minimum sale price of {listingCurrency} {demarcation.TargetSalePrice.Value:N2}.");
        }
        if (publishToCustomerPortal && includesRent && monthlyRent is not > 0m)
        {
            throw new InvalidOperationException("Enter the monthly rent before listing this demarcation.");
        }

        demarcation.IsReadyForProjectManagement = request.IsReadyForProjectManagement;
        demarcation.IsPublishedToExternalPortal = request.IsPublishedToExternalPortal;
        demarcation.ExternalListingType = listingType;
        demarcation.ExternalListingStatus = listingStatus;
        demarcation.ExternalSalePrice = salePrice;
        demarcation.ExternalMonthlyRent = monthlyRent;
        demarcation.ExternalLeaseTermMonths = includesRent && request.ExternalLeaseTermMonths > 0
            ? request.ExternalLeaseTermMonths
            : null;
        demarcation.ExternalListingPrice = includesSale ? salePrice : monthlyRent;
        demarcation.ExternalListingCurrency = listingCurrency;
        demarcation.ExternalListingNotes = TrimOrNull(request.ExternalListingNotes);
        demarcation.ParentLandAssetReference = asset.AssetCode;
        demarcation.ParentFixedAssetReference = BuildParentFixedAssetReference(
            asset,
            demarcations.FirstOrDefault(item => item.Id == demarcation.ParentDemarcationId));
        demarcation.ChildFixedAssetReference = BuildChildFixedAssetReference(
            demarcation.ParentFixedAssetReference!,
            demarcation.DemarcationNumber);
        demarcation.FixedAssetPostingStatus =
            request.IsReadyForProjectManagement || request.IsPublishedToExternalPortal
                ? "ReadyForPosting"
                : demarcation.AllocatedCost is > 0m
                    ? "Costed"
                    : "NotReady";
        demarcation.ExternalPublishedAt = publishToCustomerPortal
            ? demarcation.ExternalPublishedAt ?? DateTime.UtcNow
            : null;
        demarcation.UpdatedAt = DateTime.UtcNow;
        demarcation.UpdatedBy = _currentUserProvider.Username;
        demarcation.LastModifiedById = _currentUserProvider.UserId;

        await repository.UpdateAsync(demarcation);
        await _unitOfWork.SaveChangesAsync();
        return MapDemarcationToDto(demarcation, asset.AssetCode);
    }

    public Task<EstateLandDemarcationDto> UpdateLandDemarcationCostingAsync(
        Guid assetId,
        Guid demarcationId,
        UpdateEstateLandDemarcationCostingDto request)
        => ExecuteSerializableMutationAsync(
            () => UpdateLandDemarcationCostingCoreAsync(assetId, demarcationId, request));

    private async Task<EstateLandDemarcationDto> UpdateLandDemarcationCostingCoreAsync(
        Guid assetId,
        Guid demarcationId,
        UpdateEstateLandDemarcationCostingDto request)
    {
        var asset = await RequireLandAssetAsync(assetId);
        var repository = _unitOfWork.Repository<EstateLandDemarcation>();
        var demarcations = (await repository.FindAsync(item =>
                item.EstateManagedAssetId == assetId
                && item.TenantId == _currentUserProvider.TenantId
                && !item.IsDeleted))
            .ToList();
        var demarcation = demarcations.FirstOrDefault(item => item.Id == demarcationId)
            ?? throw new InvalidOperationException("Land demarcation was not found.");

        var demarcationAcres = demarcation.AreaSquareFeet / 43560m;
        if (demarcationAcres <= 0m)
        {
            throw new InvalidOperationException("The demarcation area must be available before costing.");
        }

        var method = NormalizeDemarcationCostMethod(request.CostAllocationMethod);
        decimal? allocatedCost = null;
        decimal? costPerAcre = null;
        if (method == "ByArea")
        {
            costPerAcre = CalculateRemainingByAreaCostPerAcre(
                asset,
                demarcations,
                demarcation.ParentDemarcationId,
                demarcation.Id);
            allocatedCost = decimal.Round(
                costPerAcre.Value * demarcationAcres,
                2,
                MidpointRounding.AwayFromZero);
        }
        else if (method == "Manual")
        {
            allocatedCost = request.AllocatedCost;
            if (allocatedCost is not > 0m)
            {
                throw new InvalidOperationException("Enter the demarcation cost before saving manual costing.");
            }

            costPerAcre = request.CostPerAcre is > 0m
                ? request.CostPerAcre
                : decimal.Round(allocatedCost.Value / demarcationAcres, 2, MidpointRounding.AwayFromZero);
        }

        var targetSalePrice = request.TargetSalePrice is > 0m
            ? request.TargetSalePrice
            : null;
        demarcation.CostAllocationMethod = method;
        demarcation.AllocatedCost = allocatedCost;
        demarcation.CostPerAcre = costPerAcre;
        demarcation.TargetSalePrice = targetSalePrice;
        demarcation.ParentLandAssetReference = asset.AssetCode;
        demarcation.ParentFixedAssetReference = BuildParentFixedAssetReference(
            asset,
            demarcations.FirstOrDefault(item => item.Id == demarcation.ParentDemarcationId));
        demarcation.ChildFixedAssetReference = BuildChildFixedAssetReference(
            demarcation.ParentFixedAssetReference!,
            demarcation.DemarcationNumber);
        demarcation.FixedAssetPostingStatus = allocatedCost is > 0m
            ? "Costed"
            : "NotReady";
        if (targetSalePrice is > 0m)
        {
            demarcation.ExternalSalePrice = demarcation.ExternalSalePrice ?? targetSalePrice;
            demarcation.ExternalListingPrice = demarcation.ExternalListingPrice ?? targetSalePrice;
        }
        RebalanceDemarcationCostGroup(asset, demarcations, demarcation.ParentDemarcationId);
        demarcation.UpdatedAt = DateTime.UtcNow;
        demarcation.UpdatedBy = _currentUserProvider.Username;
        demarcation.LastModifiedById = _currentUserProvider.UserId;

        await repository.UpdateAsync(demarcation);
        await _unitOfWork.SaveChangesAsync();
        return MapDemarcationToDto(demarcation, asset.AssetCode);
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
        if (activeDemarcations.Any(item => item.ParentDemarcationId == demarcation.Id))
        {
            throw new InvalidOperationException(
                "Delete child demarcations before deleting this parent parcel.");
        }

        demarcation.IsDeleted = true;
        demarcation.UpdatedAt = DateTime.UtcNow;
        demarcation.UpdatedBy = _currentUserProvider.Username;
        demarcation.LastModifiedById = _currentUserProvider.UserId;
        RebalanceDemarcationCostGroup(
            asset,
            activeDemarcations.Where(item => item.Id != demarcation.Id).ToList(),
            demarcation.ParentDemarcationId);
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
        if (asset.IsPublishedToExternalPortal || asset.ExternalListingType != "None")
            throw new InvalidOperationException("Remove the land from Portal Listings before marking it ready for project management.");
        if (!asset.BoundaryVerified || string.IsNullOrWhiteSpace(asset.BoundaryCoordinates))
            throw new InvalidOperationException("Verify and record the cadastral boundary before project handoff.");
        var demarcations = await _unitOfWork.Repository<EstateLandDemarcation>()
            .GetQueryable(item => item.EstateManagedAssetId == assetId
                && item.TenantId == _currentUserProvider.TenantId
                && !item.IsDeleted)
            .ToListAsync();
        if (demarcations.Count == 0)
            throw new InvalidOperationException("Add at least one demarcation within the main cadastral boundary.");
        if (demarcations.Any(item => !item.BoundaryVerified))
            throw new InvalidOperationException("Verify every demarcation before project handoff.");
        EnsureDemarcationCostingReconcilesForOutbound(asset, demarcations);
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

    public Task<EstateManagedAssetDto> UpdateRegisterAsync(
        Guid assetId,
        UpdateEstateManagedAssetRegisterDto request)
        => ExecuteSerializableMutationAsync(
            () => UpdateRegisterCoreAsync(assetId, request));

    private async Task<EstateManagedAssetDto> UpdateRegisterCoreAsync(
        Guid assetId,
        UpdateEstateManagedAssetRegisterDto request)
    {
        if (request.LeaseTermYears is <= 0 or > 999)
        {
            throw new InvalidOperationException("Lease term must be between 1 and 999 years.");
        }

        var repository = _unitOfWork.Repository<EstateManagedAsset>();
        var asset = await repository.FirstOrDefaultAsync(item =>
            item.Id == assetId
            && item.TenantId == _currentUserProvider.TenantId
            && !item.IsDeleted);
        if (asset == null)
        {
            throw new InvalidOperationException("Estate asset was not found.");
        }

        var isExistingLeaseRecord = asset.CustomerBusinessPartnerId.HasValue
            && asset.CustomerBusinessPartnerId == request.CustomerBusinessPartnerId
            && asset.Status is EstateManagedAssetStatus.Reserved
                or EstateManagedAssetStatus.Leased
                or EstateManagedAssetStatus.Occupied;
        if (!isExistingLeaseRecord
            && (asset.Status != EstateManagedAssetStatus.Available
                || !asset.IsAvailableForLease))
        {
            throw new InvalidOperationException(
                "Only land, property, or units released as available for lease can be assigned to a customer.");
        }

        if (asset.CustomerBusinessPartnerId.HasValue
            && asset.CustomerBusinessPartnerId != request.CustomerBusinessPartnerId)
        {
            throw new InvalidOperationException(
                "This asset already has a customer assignment. Manage changes through its existing lease record.");
        }

        BusinessPartner? customer = null;
        if (request.CustomerBusinessPartnerId.HasValue)
        {
            customer = await _unitOfWork.Repository<BusinessPartner>()
                .FirstOrDefaultAsync(item =>
                    item.Id == request.CustomerBusinessPartnerId.Value
                    && item.TenantId == _currentUserProvider.TenantId
                    && !item.IsDeleted);
            if (customer == null)
            {
                throw new InvalidOperationException("The linked customer was not found.");
            }

            if (!customer.IsActive
                || !string.Equals(customer.PartnerType, "Customer", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Select an active customer business partner for this lease.");
            }
        }

        var hasSignedAgreementReference = !string.IsNullOrWhiteSpace(request.PropertyFileReference);
        var hasBillingStartDate = request.DateOfTenancy.HasValue || request.RightOfEntryDate.HasValue;
        if (hasSignedAgreementReference && !hasBillingStartDate)
        {
            throw new InvalidOperationException(
                "Record the agreement start date or right-of-entry / move-in date with the signed agreement reference.");
        }

        asset.DateOfTenancy = request.DateOfTenancy;
        asset.RightOfEntryDate = request.RightOfEntryDate;
        asset.LeaseTermYears = request.LeaseTermYears;
        asset.CustomerBusinessPartnerId = request.CustomerBusinessPartnerId;
        asset.LesseeName = TrimOrNull(request.LesseeName)
            ?? TrimOrNull(customer?.PartnerName);
        asset.LesseeAddress = TrimOrNull(request.LesseeAddress)
            ?? TrimOrNull(customer?.PhysicalAddress);
        asset.PropertyFileReference = TrimOrNull(request.PropertyFileReference);
        if (request.CustomerBusinessPartnerId.HasValue
            && asset.Status is EstateManagedAssetStatus.Available
                or EstateManagedAssetStatus.Reserved
                or EstateManagedAssetStatus.Leased)
        {
            asset.Status = hasSignedAgreementReference && hasBillingStartDate
                ? EstateManagedAssetStatus.Leased
                : EstateManagedAssetStatus.Reserved;
        }

        if (request.CustomerBusinessPartnerId.HasValue)
        {
            asset.IsAvailableForLease = false;
            asset.IsAvailableForSale = false;
            asset.IsPublishedToExternalPortal = false;
            asset.ExternalListingStatus = "Withdrawn";
            asset.ExternalPublishedAt = null;
        }

        asset.UpdatedAt = DateTime.UtcNow;
        asset.UpdatedBy = _currentUserProvider.Username;
        asset.LastModifiedById = _currentUserProvider.UserId;

        await repository.UpdateAsync(asset);
        await _unitOfWork.SaveChangesAsync();
        return MapToDto(asset);
    }

    public Task<EstateManagedAssetDto> UpdateOccupancyAsync(
        Guid assetId,
        UpdateEstateManagedAssetOccupancyDto request)
        => ExecuteSerializableMutationAsync(
            () => UpdateOccupancyCoreAsync(assetId, request));

    private async Task<EstateManagedAssetDto> UpdateOccupancyCoreAsync(
        Guid assetId,
        UpdateEstateManagedAssetOccupancyDto request)
    {
        if (request.ActualDate.HasValue
            && request.ActualDate.Value.Date > DateTime.UtcNow.Date)
        {
            throw new InvalidOperationException("The actual handover date cannot be in the future.");
        }

        var repository = _unitOfWork.Repository<EstateManagedAsset>();
        var asset = await repository.FirstOrDefaultAsync(item =>
            item.Id == assetId
            && item.TenantId == _currentUserProvider.TenantId
            && !item.IsDeleted);
        if (asset == null)
        {
            throw new InvalidOperationException("Estate asset was not found.");
        }

        if (asset.Status == EstateManagedAssetStatus.Sold
            && request.Status != EstateManagedAssetStatus.Sold)
        {
            throw new InvalidOperationException(
                "Sold assets cannot be reopened from Occupancy / Availability.");
        }

        var hasOccupant = asset.CustomerBusinessPartnerId.HasValue
            || !string.IsNullOrWhiteSpace(asset.LesseeName);
        if (request.ReleaseOccupant == true
            && request.Status != EstateManagedAssetStatus.Available)
        {
            throw new InvalidOperationException(
                "An occupant can only be released when returning the asset to available status.");
        }

        if (request.Status is EstateManagedAssetStatus.Reserved
                or EstateManagedAssetStatus.Leased
                or EstateManagedAssetStatus.Occupied
            && !hasOccupant)
        {
            throw new InvalidOperationException(
                "Reserve, lease, or occupy the asset only after a customer or occupant is linked in Lease Management.");
        }

        if (request.Status is EstateManagedAssetStatus.Leased
                or EstateManagedAssetStatus.Occupied
            && (string.IsNullOrWhiteSpace(asset.PropertyFileReference)
                || (!asset.DateOfTenancy.HasValue && !asset.RightOfEntryDate.HasValue)))
        {
            throw new InvalidOperationException(
                "Record the signed agreement reference and agreement start or move-in date in Lease Management before marking this asset leased or occupied.");
        }

        if (request.Status == EstateManagedAssetStatus.Available
            && hasOccupant
            && request.ReleaseOccupant != true)
        {
            throw new InvalidOperationException(
                "Release the lease or occupant link before marking this asset available.");
        }

        if (request.Status == EstateManagedAssetStatus.Available
            && request.ReleaseOccupant == true
            && !request.ActualDate.HasValue)
        {
            throw new InvalidOperationException(
                "Record the actual move-out date before releasing the occupant.");
        }

        if (request.Status == EstateManagedAssetStatus.LandBank
            && asset.AssetType != EstateManagedAssetType.Land)
        {
            throw new InvalidOperationException("Only land assets can return to Land Bank status.");
        }

        asset.Status = request.Status;
        var requestedNotes = TrimOrNull(request.Notes);
        if (request.Status == EstateManagedAssetStatus.Available
            && request.ReleaseOccupant == true)
        {
            var releaseHistory = string.Join(
                " | ",
                new[]
                {
                    asset.Notes,
                    requestedNotes,
                    $"Occupancy released on {request.ActualDate!.Value:yyyy-MM-dd}; "
                    + $"occupant: {asset.LesseeName ?? "not recorded"}; "
                    + $"agreement: {asset.PropertyFileReference ?? "not recorded"}"
                }.Where(value => !string.IsNullOrWhiteSpace(value)));

            asset.Notes = releaseHistory;
            asset.CustomerBusinessPartnerId = null;
            asset.LesseeName = null;
            asset.LesseeAddress = null;
            asset.DateOfTenancy = null;
            asset.RightOfEntryDate = null;
            asset.LeaseTermYears = null;
            asset.PropertyFileReference = null;
            asset.RentBillingActivatedAt = null;
            asset.NextRentBillingDate = null;
            asset.AutoGenerateRentInvoices = false;
        }
        else
        {
            asset.Notes = requestedNotes ?? asset.Notes;
        }

        if (request.Status == EstateManagedAssetStatus.Occupied)
        {
            if (!request.ActualDate.HasValue)
            {
                throw new InvalidOperationException(
                    "Record the actual possession date before marking this asset occupied.");
            }

            asset.RightOfEntryDate = request.ActualDate.Value.Date;
        }

        if (request.Status == EstateManagedAssetStatus.Available)
        {
            asset.IsAvailableForLease = request.IsAvailableForLease ?? asset.IsAvailableForLease;
            asset.IsAvailableForSale = request.IsAvailableForSale ?? asset.IsAvailableForSale;
            if (request.IsPublishedToExternalPortal == false
                || (!asset.IsAvailableForLease && !asset.IsAvailableForSale))
            {
                asset.IsPublishedToExternalPortal = false;
                asset.ExternalListingStatus = !asset.IsAvailableForLease && !asset.IsAvailableForSale
                    ? "Draft"
                    : "Withdrawn";
                asset.ExternalPublishedAt = null;
            }
        }
        else if (request.Status is EstateManagedAssetStatus.LandBank)
        {
            asset.IsAvailableForLease = false;
            asset.IsAvailableForSale = false;
            asset.IsPublishedToExternalPortal = false;
            asset.ExternalListingStatus = "Withdrawn";
            asset.ExternalPublishedAt = null;
        }
        else
        {
            asset.IsAvailableForLease = false;
            asset.IsAvailableForSale = false;
            asset.IsPublishedToExternalPortal = false;
            asset.ExternalListingStatus = request.Status == EstateManagedAssetStatus.Sold
                ? "Sold"
                : "Withdrawn";
            asset.ExternalPublishedAt = null;
        }

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

        var isPortalListing = listingType != "None";
        if (isPortalListing && asset.AssetType == EstateManagedAssetType.Land)
        {
            if (asset.Status != EstateManagedAssetStatus.LandBank || asset.ProjectId.HasValue)
            {
                throw new InvalidOperationException("Land assigned to a development project cannot be sent to Portal Listings.");
            }
            if (!asset.BoundaryVerified)
            {
                throw new InvalidOperationException("Verify the main cadastral boundary before sending land to Portal Listings.");
            }

            var demarcations = (await _unitOfWork.Repository<EstateLandDemarcation>().FindAsync(item =>
                    item.EstateManagedAssetId == assetId
                    && item.TenantId == _currentUserProvider.TenantId
                    && !item.IsDeleted))
                .ToList();
            if (!demarcations.Any() || demarcations.Any(item => !item.BoundaryVerified))
            {
                throw new InvalidOperationException("Add and verify every land demarcation before sending land to Portal Listings.");
            }
            var assignedLandReferences = await GetAssignedProjectLandReferencesAsync();
            if (demarcations.Any(item => IsDemarcationAssignedToProject(
                    asset,
                    demarcations,
                    item,
                    assignedLandReferences)))
            {
                throw new InvalidOperationException(
                    "Land with a demarcated portion assigned to a development project cannot be sent to Portal Listings.");
            }

            // Portal listing and project handoff are exclusive choices. Staging the land in
            // Portal Listings removes it from the project-ready pool before publication.
            asset.IsReadyForProjectManagement = false;
        }
        else if (isPortalListing
            && (asset.AssetType == EstateManagedAssetType.Property
                || asset.AssetType == EstateManagedAssetType.Facility))
        {
            if (asset.SourceType != EstateManagedAssetSourceType.ProjectUnit
                || !asset.IsPublishedFromProject)
            {
                throw new InvalidOperationException(
                    "Only property handed off from Project Management can be sent to Portal Listings.");
            }

            if (asset.Status != EstateManagedAssetStatus.Available)
            {
                throw new InvalidOperationException(
                    "Only an available project-handoff property can be listed on the external portal.");
            }
        }

        var includesSale = listingType is "Sale" or "SaleAndRent";
        var includesRent = listingType is "Rent" or "SaleAndRent";
        var requestedSalePrice = request.ExternalSalePrice
            ?? (includesSale ? request.ExternalListingPrice : null);
        var requestedMonthlyRent = request.ExternalMonthlyRent
            ?? (includesRent && !includesSale ? request.ExternalListingPrice : null);
        var salePrice = includesSale && requestedSalePrice > 0
            ? requestedSalePrice
            : null;
        var monthlyRent = includesRent && requestedMonthlyRent > 0
            ? requestedMonthlyRent
            : null;
        var leaseTermMonths = includesRent && request.ExternalLeaseTermMonths > 0
            ? request.ExternalLeaseTermMonths
            : null;

        if (request.IsPublishedToExternalPortal && includesRent)
        {
            if (!monthlyRent.HasValue)
            {
                throw new InvalidOperationException("Enter the monthly rent before publishing a rental listing.");
            }

            if (asset.AssetType == EstateManagedAssetType.Land
                && asset.GroundRentPayable is not > 0m)
            {
                throw new InvalidOperationException(
                    "Assess and approve the annual ground rent before publishing a land rental listing.");
            }

            if (!leaseTermMonths.HasValue || leaseTermMonths > 1200)
            {
                throw new InvalidOperationException(
                    "Enter a rental duration between 1 and 1,200 months before publishing.");
            }
        }

        if (request.IsPublishedToExternalPortal && includesSale && !salePrice.HasValue)
        {
            throw new InvalidOperationException("Enter the sale price before publishing a sale listing.");
        }

        asset.IsPublishedToExternalPortal = request.IsPublishedToExternalPortal;
        asset.ExternalListingType = listingType;
        asset.ExternalListingStatus = request.IsPublishedToExternalPortal ? NormalizeListingStatus(request.ExternalListingStatus) : "Draft";
        asset.ExternalSalePrice = salePrice;
        asset.ExternalMonthlyRent = monthlyRent;
        asset.ExternalLeaseTermMonths = leaseTermMonths;
        // Keep the legacy price populated for older integrations while the portal uses
        // the explicit sale and monthly-rent values.
        asset.ExternalListingPrice = includesSale ? salePrice : monthlyRent;
        asset.ExternalListingCurrency = string.IsNullOrWhiteSpace(request.ExternalListingCurrency)
            ? "GHS"
            : request.ExternalListingCurrency.Trim().ToUpperInvariant();
        asset.ExternalListingNotes = TrimOrNull(request.ExternalListingNotes);
        asset.ExternalPublishedAt = request.IsPublishedToExternalPortal
            ? asset.ExternalPublishedAt ?? DateTime.UtcNow
            : null;
        asset.IsAvailableForSale = includesSale;
        asset.IsAvailableForLease = includesRent;
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
        SaveEstateLandDemarcationDto request,
        EstateLandDemarcation? parentDemarcation = null)
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

        var containingBoundary = parentDemarcation?.BoundaryCoordinates ?? asset.BoundaryCoordinates;
        var measurement = EstateBoundaryGeometry.ValidateContained(
            containingBoundary,
            request.BoundaryCoordinates);
        if (request.BeaconCount != measurement.BeaconCount)
        {
            throw new InvalidOperationException("The beacon count does not match the demarcation coordinates.");
        }

        return measurement;
    }

    private async Task<EstateLandDemarcation?> ResolveParentDemarcationAsync(
        Guid assetId,
        Guid? parentDemarcationId)
    {
        if (!parentDemarcationId.HasValue)
        {
            return null;
        }

        var parent = await _unitOfWork.Repository<EstateLandDemarcation>()
            .FirstOrDefaultAsync(item =>
                item.Id == parentDemarcationId.Value
                && item.EstateManagedAssetId == assetId
                && item.TenantId == _currentUserProvider.TenantId
                && !item.IsDeleted);
        if (parent == null)
        {
            throw new InvalidOperationException("The parent demarcation was not found.");
        }

        if (!parent.BoundaryVerified)
        {
            throw new InvalidOperationException("Verify the parent demarcation before subdividing it.");
        }

        if (parent.IsReadyForProjectManagement || parent.IsPublishedToExternalPortal)
        {
            throw new InvalidOperationException("Clear the parent demarcation's project or portal status before subdividing it.");
        }

        return parent;
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

    private async Task ResetDemarcationReadinessAsync(
        EstateManagedAsset asset,
        EstateLandDemarcation? parentDemarcation)
    {
        if (parentDemarcation == null)
        {
            await ResetProjectReadinessAsync(asset);
            return;
        }

        parentDemarcation.IsReadyForProjectManagement = false;
        parentDemarcation.IsPublishedToExternalPortal = false;
        parentDemarcation.ExternalListingType = "None";
        parentDemarcation.ExternalListingStatus = "Draft";
        parentDemarcation.ExternalPublishedAt = null;
        parentDemarcation.UpdatedAt = DateTime.UtcNow;
        parentDemarcation.UpdatedBy = _currentUserProvider.Username;
        parentDemarcation.LastModifiedById = _currentUserProvider.UserId;
        await _unitOfWork.Repository<EstateLandDemarcation>().UpdateAsync(parentDemarcation);
    }

    private static void EnsureDemarcationsCanBeChanged(EstateManagedAsset asset)
    {
        if (asset.IsPublishedToExternalPortal)
        {
            throw new InvalidOperationException(
                "Withdraw the active external land listing before changing its demarcations.");
        }
    }

    private static string NormalizeDemarcationCostMethod(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().Replace(" ", string.Empty);
        return normalized.Equals("Manual", StringComparison.OrdinalIgnoreCase)
            ? "Manual"
            : normalized.Equals("NotSet", StringComparison.OrdinalIgnoreCase) ||
              normalized.Equals("None", StringComparison.OrdinalIgnoreCase)
                ? "NotSet"
                : "ByArea";
    }

    private static void ApplyAutomaticDemarcationCosting(
        EstateManagedAsset asset,
        IReadOnlyCollection<EstateLandDemarcation> demarcations,
        EstateLandDemarcation demarcation,
        decimal? targetSalePrice)
    {
        var demarcationAcres = GetDemarcationAreaInAcres(demarcation);
        var excludedDemarcationId = demarcation.Id == Guid.Empty
            ? (Guid?)null
            : demarcation.Id;
        var costPerAcre = CalculateRemainingByAreaCostPerAcre(
            asset,
            demarcations,
            demarcation.ParentDemarcationId,
            excludedDemarcationId);

        demarcation.CostAllocationMethod = "ByArea";
        demarcation.CostPerAcre = costPerAcre;
        demarcation.AllocatedCost = decimal.Round(
            costPerAcre * demarcationAcres,
            2,
            MidpointRounding.AwayFromZero);
        demarcation.TargetSalePrice = targetSalePrice is > 0m ? targetSalePrice : null;
        demarcation.FixedAssetPostingStatus = "Costed";
    }

    private static decimal CalculateByAreaCostPerAcre(
        EstateManagedAsset asset,
        IReadOnlyCollection<EstateLandDemarcation> demarcations,
        Guid? parentDemarcationId)
        => CalculateRemainingByAreaCostPerAcre(asset, demarcations, parentDemarcationId);

    private static decimal CalculateRemainingByAreaCostPerAcre(
        EstateManagedAsset asset,
        IReadOnlyCollection<EstateLandDemarcation> demarcations,
        Guid? parentDemarcationId,
        Guid? excludedDemarcationId = null)
    {
        var parentCost = GetDemarcationParentCostPool(asset, demarcations, parentDemarcationId);
        var parentArea = GetDemarcationParentAreaInAcres(asset, demarcations, parentDemarcationId);
        var manualSiblings = demarcations
            .Where(item =>
                item.ParentDemarcationId == parentDemarcationId &&
                item.Id != excludedDemarcationId &&
                IsManualCostedDemarcation(item))
            .ToList();
        var manualCost = manualSiblings.Sum(item => item.AllocatedCost!.Value);
        if (manualCost > parentCost)
        {
            throw new InvalidOperationException(
                $"Manual demarcation costs exceed the parent land value. Allocated {manualCost:N2}, parent value {parentCost:N2}.");
        }

        var manualArea = manualSiblings.Sum(GetDemarcationAreaInAcres);
        var remainingCost = parentCost - manualCost;
        var remainingArea = parentArea - manualArea;
        if (remainingCost <= 0m || remainingArea <= 0m)
        {
            throw new InvalidOperationException(
                "No remaining land value is available for by-area costing. Lower a manual demarcation cost first.");
        }

        return decimal.Round(remainingCost / remainingArea, 2, MidpointRounding.AwayFromZero);
    }

    private static void RebalanceDemarcationCostGroup(
        EstateManagedAsset asset,
        IReadOnlyCollection<EstateLandDemarcation> demarcations,
        Guid? parentDemarcationId)
    {
        var siblings = demarcations
            .Where(item => item.ParentDemarcationId == parentDemarcationId)
            .OrderBy(item => item.DemarcationNumber)
            .ToList();
        if (siblings.Count == 0)
        {
            return;
        }

        var parentCost = GetDemarcationParentCostPool(asset, demarcations, parentDemarcationId);
        var parentArea = GetDemarcationParentAreaInAcres(asset, demarcations, parentDemarcationId);
        var manualSiblings = siblings.Where(IsManualCostedDemarcation).ToList();
        var manualCost = manualSiblings.Sum(item => item.AllocatedCost!.Value);
        if (manualCost > parentCost)
        {
            throw new InvalidOperationException(
                $"Manual demarcation costs exceed the parent land value. Allocated {manualCost:N2}, parent value {parentCost:N2}. Lower a manual demarcation cost before saving.");
        }

        var manualArea = manualSiblings.Sum(GetDemarcationAreaInAcres);
        var remainingCost = parentCost - manualCost;
        var remainingArea = parentArea - manualArea;
        var byAreaSiblings = siblings
            .Where(item => !IsManualCostedDemarcation(item))
            .ToList();
        var byAreaArea = byAreaSiblings.Sum(GetDemarcationAreaInAcres);
        if (byAreaSiblings.Count > 0)
        {
            if (remainingCost <= 0m || remainingArea <= 0m)
            {
                throw new InvalidOperationException(
                    "No remaining land value is available for by-area demarcations. Lower a manual demarcation cost before saving.");
            }

            var rate = decimal.Round(remainingCost / remainingArea, 2, MidpointRounding.AwayFromZero);
            var allocatedByArea = 0m;
            for (var index = 0; index < byAreaSiblings.Count; index++)
            {
                var child = byAreaSiblings[index];
                var childAcres = GetDemarcationAreaInAcres(child);
                var coversRemainingArea =
                    byAreaArea >= remainingArea - 0.0001m &&
                    index == byAreaSiblings.Count - 1;
                var allocatedCost = coversRemainingArea
                    ? decimal.Round(remainingCost - allocatedByArea, 2, MidpointRounding.AwayFromZero)
                    : decimal.Round(rate * childAcres, 2, MidpointRounding.AwayFromZero);

                child.CostAllocationMethod = "ByArea";
                child.CostPerAcre = rate;
                child.AllocatedCost = allocatedCost;
                child.FixedAssetPostingStatus = allocatedCost > 0m ? "Costed" : "NotReady";
                allocatedByArea += allocatedCost;
            }
        }

        foreach (var child in siblings.Where(item => demarcations.Any(candidate => candidate.ParentDemarcationId == item.Id)))
        {
            RebalanceDemarcationCostGroup(asset, demarcations, child.Id);
        }
    }

    private static decimal GetDemarcationParentCostPool(
        EstateManagedAsset asset,
        IReadOnlyCollection<EstateLandDemarcation> demarcations,
        Guid? parentDemarcationId)
    {
        if (parentDemarcationId.HasValue)
        {
            var parent = demarcations.FirstOrDefault(item => item.Id == parentDemarcationId.Value)
                ?? throw new InvalidOperationException("Parent demarcation was not found.");
            if (parent.AllocatedCost is not > 0m)
            {
                throw new InvalidOperationException(
                    "Set the parent parcel cost before calculating child parcel cost by area.");
            }

            return parent.AllocatedCost.Value;
        }

        var totalCost = asset.ValuationAmount ?? 0m;
        if (totalCost <= 0m)
        {
            throw new InvalidOperationException(
                "Record the land total valuation or capitalized cost before calculating demarcation cost by area.");
        }

        return totalCost;
    }

    private static decimal GetDemarcationParentAreaInAcres(
        EstateManagedAsset asset,
        IReadOnlyCollection<EstateLandDemarcation> demarcations,
        Guid? parentDemarcationId)
    {
        if (parentDemarcationId.HasValue)
        {
            var parent = demarcations.FirstOrDefault(item => item.Id == parentDemarcationId.Value)
                ?? throw new InvalidOperationException("Parent demarcation was not found.");
            return GetDemarcationAreaInAcres(parent);
        }

        var totalAcres = GetAssetAreaInAcres(asset);
        if (totalAcres <= 0m)
        {
            throw new InvalidOperationException(
                "Record the land area before calculating demarcation cost by area.");
        }

        return totalAcres;
    }

    private static bool IsManualCostedDemarcation(EstateLandDemarcation demarcation)
        => string.Equals(
                demarcation.CostAllocationMethod,
                "Manual",
                StringComparison.OrdinalIgnoreCase) &&
            demarcation.AllocatedCost is > 0m;

    private static decimal GetAssetAreaInAcres(EstateManagedAsset asset)
    {
        if (asset.AreaValue is > 0m)
        {
            var unit = (asset.AreaUnit ?? string.Empty).Trim().ToLowerInvariant();
            if (unit.Contains("acre")) return asset.AreaValue.Value;
            if (unit.Contains("hectare") || unit == "ha") return asset.AreaValue.Value * 2.4710538147m;
            if (unit.Contains("square meter") || unit.Contains("sqm") || unit == "m2")
                return asset.AreaValue.Value / 4046.8564224m;
            if (unit.Contains("square feet") || unit.Contains("sq ft") || unit.Contains("sqft") || unit == "ft2")
                return asset.AreaValue.Value / 43560m;

            return asset.AreaValue.Value;
        }

        return asset.AreaSquareMeters is > 0m
            ? asset.AreaSquareMeters.Value / 4046.8564224m
            : 0m;
    }

    private static decimal GetDemarcationAreaInAcres(EstateLandDemarcation demarcation)
    {
        var acres = demarcation.AreaSquareFeet / 43560m;
        if (acres <= 0m)
        {
            throw new InvalidOperationException("The demarcation area must be available before costing.");
        }

        return acres;
    }

    private static string BuildParentFixedAssetReference(
        EstateManagedAsset asset,
        EstateLandDemarcation? parent)
        => parent?.ChildFixedAssetReference
            ?? (parent == null
                ? asset.AssetCode
                : BuildChildFixedAssetReference(asset.AssetCode, parent.DemarcationNumber));

    private static string BuildChildFixedAssetReference(string parentFixedAssetReference, int demarcationNumber)
        => $"{parentFixedAssetReference}-D{demarcationNumber:000}";

    private static IReadOnlyList<EstateLandDemarcation> GetLeafDemarcations(
        IReadOnlyCollection<EstateLandDemarcation> demarcations)
    {
        var parentIds = demarcations
            .Where(item => item.ParentDemarcationId.HasValue)
            .Select(item => item.ParentDemarcationId!.Value)
            .ToHashSet();
        return demarcations
            .Where(item => !parentIds.Contains(item.Id))
            .ToList();
    }

    private static IReadOnlyList<EstateLandDemarcation> GetLeafDescendants(
        IReadOnlyCollection<EstateLandDemarcation> demarcations,
        Guid parentDemarcationId)
    {
        var leaves = new List<EstateLandDemarcation>();
        var children = demarcations
            .Where(item => item.ParentDemarcationId == parentDemarcationId)
            .ToList();

        foreach (var child in children)
        {
            if (demarcations.Any(item => item.ParentDemarcationId == child.Id))
            {
                leaves.AddRange(GetLeafDescendants(demarcations, child.Id));
            }
            else
            {
                leaves.Add(child);
            }
        }

        return leaves;
    }

    private static void EnsureDemarcationCostingReconcilesForOutbound(
        EstateManagedAsset asset,
        IReadOnlyCollection<EstateLandDemarcation> demarcations,
        EstateLandDemarcation? selectedDemarcation = null)
    {
        var expectedTotal = asset.ValuationAmount ?? 0m;
        if (expectedTotal <= 0m)
        {
            throw new InvalidOperationException(
                "Record the parent land capitalized value before pushing demarcations onward.");
        }

        var leafDemarcations = GetLeafDemarcations(demarcations);
        if (leafDemarcations.Count == 0)
        {
            throw new InvalidOperationException("Add demarcations before pushing land onward.");
        }

        var missingCost = leafDemarcations
            .Where(item => item.AllocatedCost is not > 0m)
            .Select(item => $"Parcel {item.DemarcationNumber}")
            .ToList();
        if (missingCost.Count > 0)
        {
            throw new InvalidOperationException(
                $"Set costs for every final demarcated parcel before pushing onward. Missing: {string.Join(", ", missingCost)}.");
        }

        var parents = demarcations
            .Where(parent => demarcations.Any(child => child.ParentDemarcationId == parent.Id))
            .ToList();
        foreach (var parent in parents)
        {
            if (parent.AllocatedCost is not > 0m)
            {
                throw new InvalidOperationException(
                    $"Set the parent cost for parcel {parent.DemarcationNumber} before pushing its child parcels onward.");
            }
        }

        foreach (var parent in parents)
        {
            var parentLeaves = GetLeafDescendants(demarcations, parent.Id);
            var allocatedToChildren = decimal.Round(
                parentLeaves.Sum(item => item.AllocatedCost!.Value),
                2,
                MidpointRounding.AwayFromZero);
            var parentCost = decimal.Round(parent.AllocatedCost!.Value, 2, MidpointRounding.AwayFromZero);
            if (allocatedToChildren != parentCost)
            {
                throw new InvalidOperationException(
                    $"Demarcation costs for parcel {parent.DemarcationNumber} must equal its parent pool. " +
                    $"Allocated {allocatedToChildren:N2}, parent pool {parentCost:N2}, " +
                    $"difference {allocatedToChildren - parentCost:N2}.");
            }
        }

        var allocatedTotal = decimal.Round(
            leafDemarcations.Sum(item => item.AllocatedCost!.Value),
            2,
            MidpointRounding.AwayFromZero);
        var roundedExpectedTotal = decimal.Round(expectedTotal, 2, MidpointRounding.AwayFromZero);
        if (allocatedTotal != roundedExpectedTotal)
        {
            throw new InvalidOperationException(
                $"Demarcation costs must equal the parent land value. Allocated {allocatedTotal:N2}, " +
                $"parent value {roundedExpectedTotal:N2}, difference {allocatedTotal - roundedExpectedTotal:N2}.");
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
                try
                {
                    if (transactionStarted && _unitOfWork.HasActiveTransaction)
                    {
                        await _unitOfWork.RollbackAsync();
                    }
                }
                finally
                {
                    _unitOfWork.ClearTrackedChanges();
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
            && HasLegacyWholeParcelAssignment(asset, assignedLandReferences);
    }

    private static bool HasLegacyWholeParcelAssignment(
        EstateManagedAsset asset,
        ISet<string> assignedLandReferences)
        => new[] { asset.AssetCode, asset.ProjectCode, asset.Name, asset.Id.ToString() }
            .Any(reference => !string.IsNullOrWhiteSpace(reference)
                && assignedLandReferences.Contains(reference.Trim()));

    private static bool IsReadyLandCandidateAssignedToProject(
        IReadOnlyCollection<ReadyLandCandidate> assetCandidates,
        ReadyLandCandidate candidate,
        ISet<string> assignedLandReferences)
    {
        var landReference = EstateLandDemarcationReference.Build(
            candidate.AssetCode,
            candidate.DemarcationNumber);
        if (assignedLandReferences.Contains(landReference))
        {
            return true;
        }

        return assetCandidates.Count == 1
            && new[]
            {
                candidate.AssetCode,
                candidate.AssetProjectCode,
                candidate.AssetName,
                candidate.AssetId.ToString()
            }.Any(reference => !string.IsNullOrWhiteSpace(reference)
                && assignedLandReferences.Contains(reference.Trim()));
    }

    private sealed class ReadyLandCandidate
    {
        public Guid AssetId { get; init; }
        public string AssetCode { get; init; } = string.Empty;
        public string AssetName { get; init; } = string.Empty;
        public string? AssetLocation { get; init; }
        public string? AssetProjectCode { get; init; }
        public EstateManagedAssetStatus AssetStatus { get; init; }
        public bool AssetIsReadyForProjectManagement { get; init; }
        public bool AssetIsPublishedToExternalPortal { get; init; }
        public Guid DemarcationId { get; init; }
        public bool DemarcationIsReadyForProjectManagement { get; init; }
        public bool DemarcationIsPublishedToExternalPortal { get; init; }
        public int DemarcationNumber { get; init; }
        public string Description { get; init; } = string.Empty;
        public decimal AreaSquareFeet { get; init; }
        public bool BoundaryVerified { get; init; }
    }

    private static EstateLandDemarcationDto MapDemarcationToDto(
        EstateLandDemarcation demarcation,
        string assetCode) => new()
    {
        Id = demarcation.Id,
        EstateManagedAssetId = demarcation.EstateManagedAssetId,
        ParentDemarcationId = demarcation.ParentDemarcationId,
        LandReference = EstateLandDemarcationReference.Build(
            assetCode,
            demarcation.DemarcationNumber),
        DemarcationNumber = demarcation.DemarcationNumber,
        Description = demarcation.Description,
        BeaconCount = demarcation.BeaconCount,
        BoundaryCoordinates = demarcation.BoundaryCoordinates,
        AreaSquareFeet = demarcation.AreaSquareFeet,
        BoundaryVerified = demarcation.BoundaryVerified,
        HasChildDemarcations = demarcation.ChildDemarcations.Any(item => !item.IsDeleted),
        CostAllocationMethod = demarcation.CostAllocationMethod,
        AllocatedCost = demarcation.AllocatedCost,
        CostPerAcre = demarcation.CostPerAcre,
        TargetSalePrice = demarcation.TargetSalePrice,
        ParentLandAssetReference = demarcation.ParentLandAssetReference,
        ParentFixedAssetReference = demarcation.ParentFixedAssetReference,
        ChildFixedAssetReference = demarcation.ChildFixedAssetReference,
        FixedAssetPostingStatus = demarcation.FixedAssetPostingStatus,
        FixedAssetPostedAt = demarcation.FixedAssetPostedAt,
        IsReadyForProjectManagement = demarcation.IsReadyForProjectManagement,
        IsPublishedToExternalPortal = demarcation.IsPublishedToExternalPortal,
        ExternalListingType = demarcation.ExternalListingType,
        ExternalListingStatus = demarcation.ExternalListingStatus,
        ExternalListingPrice = demarcation.ExternalListingPrice,
        ExternalSalePrice = demarcation.ExternalSalePrice,
        ExternalMonthlyRent = demarcation.ExternalMonthlyRent,
        ExternalLeaseTermMonths = demarcation.ExternalLeaseTermMonths,
        ExternalListingCurrency = demarcation.ExternalListingCurrency,
        ExternalListingNotes = demarcation.ExternalListingNotes,
        ExternalPublishedAt = demarcation.ExternalPublishedAt,
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
        DateOfTenancy = asset.DateOfTenancy,
        RightOfEntryDate = asset.RightOfEntryDate,
        LeaseTermYears = asset.LeaseTermYears,
        GroundRentPayable = asset.GroundRentPayable,
        GroundRentRatePerAcre = asset.GroundRentRatePerAcre,
        GroundRentComputed = asset.GroundRentComputed,
        CustomerBusinessPartnerId = asset.CustomerBusinessPartnerId,
        LesseeName = asset.LesseeName,
        LesseeAddress = asset.LesseeAddress,
        PropertyFileReference = asset.PropertyFileReference,
        AreaSquareMeters = asset.AreaSquareMeters,
        ValuationAmount = asset.ValuationAmount,
        OwnerConsiderationCost = asset.OwnerConsiderationCost,
        ExternalSurveyorCost = asset.ExternalSurveyorCost,
        StampDutyCost = asset.StampDutyCost,
        OtherAcquisitionCost = asset.OtherAcquisitionCost,
        TotalCapitalizedCost = asset.TotalCapitalizedCost,
        Currency = asset.Currency,
        IsAvailableForLease = asset.IsAvailableForLease,
        IsAvailableForSale = asset.IsAvailableForSale,
        IsPublishedFromProject = asset.IsPublishedFromProject,
        PublishedFromProjectAt = asset.PublishedFromProjectAt,
        IsPublishedToExternalPortal = asset.IsPublishedToExternalPortal,
        ExternalListingType = asset.ExternalListingType,
        ExternalListingStatus = asset.ExternalListingStatus,
        ExternalListingPrice = asset.ExternalListingPrice,
        ExternalSalePrice = asset.ExternalSalePrice,
        ExternalMonthlyRent = asset.ExternalMonthlyRent,
        RentBillingActivatedAt = asset.RentBillingActivatedAt,
        NextRentBillingDate = asset.NextRentBillingDate,
        LastRentInvoiceId = asset.LastRentInvoiceId,
        LastRentInvoiceNumber = asset.LastRentInvoiceNumber,
        AutoGenerateRentInvoices = asset.AutoGenerateRentInvoices,
        RentGracePeriodDays = asset.RentGracePeriodDays,
        RentPenaltyMethod = asset.RentPenaltyMethod,
        RentPenaltyValue = asset.RentPenaltyValue,
        RentPenaltyCapAmount = asset.RentPenaltyCapAmount,
        LastRentPenaltyInvoiceId = asset.LastRentPenaltyInvoiceId,
        LastRentPenaltyInvoiceNumber = asset.LastRentPenaltyInvoiceNumber,
        LastRentPenaltySourceInvoiceId = asset.LastRentPenaltySourceInvoiceId,
        ExternalLeaseTermMonths = asset.ExternalLeaseTermMonths,
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
