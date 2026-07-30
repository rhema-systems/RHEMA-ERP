using System.Data;
using System.Text.Json;
using ErpSystem.Core.Entities.Estate;
using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Services.Estate;

namespace ErpSystem.Core.Services.Projects;

public partial class ProjectService
{
    private static readonly (string Code, string Name, bool IsStageGateRequired)[] DefaultConstructionPhaseBlueprints =
    [
        ("FEASIBILITY", "Feasibility", true),
        ("CONCEPT_DESIGN", "Concept Design", true),
        ("DETAILED_DESIGN", "Detailed Design", true),
        ("APPROVALS", "Approvals & Permits", true),
        ("PROCUREMENT", "Procurement", true),
        ("CONSTRUCTION", "Construction", false),
        ("COMMISSIONING", "Testing & Commissioning", true),
        ("HANDOVER", "Handover", true),
        ("DEFECTS_LIABILITY", "Defects Liability", false)
    ];

    public async Task<ProjectDevelopmentProfileDto?> GetDevelopmentProfileAsync(Guid projectId)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.View);
        var profile = await _unitOfWork.Repository<ProjectDevelopmentProfile>()
            .FirstOrDefaultAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId);

        return profile == null ? null : MapToDto(profile);
    }

    public async Task<ProjectDevelopmentProfileDto> UpsertDevelopmentProfileAsync(Guid projectId, UpsertProjectDevelopmentProfileDto dto)
    {
        return await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            var transactionStarted = false;
            try
            {
                await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);
                transactionStarted = true;
                var project = await RequireProjectAsync(projectId, ProjectAccessOperation.UpdateOverview);
                var profile = await UpsertDevelopmentProfileEntityAsync(project, dto);

                if (!await HasProjectPhasesAsync(projectId))
                {
                    await SeedDefaultConstructionPhasesAsync(projectId);
                }

                await _unitOfWork.CommitAsync();
                transactionStarted = false;
                return MapToDto(profile);
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

    public async Task<IEnumerable<ProjectPhaseDto>> GetProjectPhasesAsync(Guid projectId)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.View);
        var phases = await GetProjectPhaseEntitiesAsync(projectId);
        return MapToPhaseTree(phases);
    }

    public async Task<ProjectPhaseDto> AddProjectPhaseAsync(Guid projectId, CreateProjectPhaseDto dto)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.ManagePlan);
        EnsureChronologicalDateRange(dto.PlannedStartDate, dto.PlannedEndDate, "phase");

        if (dto.ParentPhaseId.HasValue)
        {
            var parent = await GetProjectPhaseEntityAsync(dto.ParentPhaseId.Value);
            if (parent.ProjectId != projectId)
            {
                throw new InvalidOperationException("Parent phase does not belong to the selected project.");
            }
        }

        var siblings = (await _unitOfWork.Repository<ProjectPhase>().FindAsync(x =>
            x.ProjectId == projectId
            && x.TenantId == _currentUserProvider.TenantId
            && x.ParentPhaseId == dto.ParentPhaseId)).ToList();

        var entity = new ProjectPhase
        {
            TenantId = _currentUserProvider.TenantId,
            ProjectId = projectId,
            ParentPhaseId = dto.ParentPhaseId,
            Code = dto.Code?.Trim(),
            Name = dto.Name.Trim(),
            Description = TrimOrNull(dto.Description),
            Status = NormalizeProjectPhaseStatus(dto.Status),
            SortOrder = dto.SortOrder ?? (siblings.Count == 0 ? 0 : siblings.Max(x => x.SortOrder) + 1),
            IsOptional = dto.IsOptional,
            IsStageGateRequired = dto.IsStageGateRequired,
            CompletionWeightPercent = 0m,
            PlannedStartDate = dto.PlannedStartDate,
            PlannedEndDate = dto.PlannedEndDate,
            ActualStartDate = dto.ActualStartDate,
            ActualEndDate = dto.ActualEndDate,
            CreatedBy = _currentUserProvider.Username,
            CreatedById = _currentUserProvider.UserId
        };

        await _unitOfWork.Repository<ProjectPhase>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return MapToDto(entity);
    }

    public async Task<ProjectPhaseDto> UpdateProjectPhaseAsync(Guid phaseId, UpdateProjectPhaseDto dto)
    {
        var entity = await GetProjectPhaseEntityAsync(phaseId);
        await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManagePlan);
        EnsureChronologicalDateRange(dto.PlannedStartDate, dto.PlannedEndDate, "phase");

        if (dto.ParentPhaseId == phaseId)
        {
            throw new InvalidOperationException("A project phase cannot be its own parent.");
        }

        if (dto.ParentPhaseId.HasValue)
        {
            var parent = await GetProjectPhaseEntityAsync(dto.ParentPhaseId.Value);
            if (parent.ProjectId != entity.ProjectId)
            {
                throw new InvalidOperationException("Parent phase must belong to the same project.");
            }
        }

        var originalStatus = entity.Status;
        var normalizedStatus = NormalizeProjectPhaseStatus(dto.Status);
        if (!string.Equals(originalStatus, normalizedStatus, StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(normalizedStatus, ProjectPhaseStatuses.InProgress, StringComparison.OrdinalIgnoreCase))
            {
                await EnsurePhaseCanStartAsync(entity, dto.OverrideStageGate, dto.OverrideReason);
                entity.ActualStartDate ??= DateTime.UtcNow;
            }
            else if (string.Equals(normalizedStatus, ProjectPhaseStatuses.Completed, StringComparison.OrdinalIgnoreCase))
            {
                await EnsurePhaseCanCompleteAsync(entity, dto.OverrideStageGate, dto.OverrideReason);
                entity.ActualStartDate ??= DateTime.UtcNow;
                entity.ActualEndDate ??= DateTime.UtcNow;
            }
        }

        entity.ParentPhaseId = dto.ParentPhaseId;
        entity.Code = dto.Code?.Trim();
        entity.Name = dto.Name.Trim();
        entity.Description = TrimOrNull(dto.Description);
        entity.Status = normalizedStatus;
        entity.SortOrder = dto.SortOrder ?? entity.SortOrder;
        entity.IsOptional = dto.IsOptional;
        entity.IsStageGateRequired = dto.IsStageGateRequired;
        entity.PlannedStartDate = dto.PlannedStartDate;
        entity.PlannedEndDate = dto.PlannedEndDate;
        entity.ActualStartDate = dto.ActualStartDate ?? entity.ActualStartDate;
        entity.ActualEndDate = dto.ActualEndDate ?? entity.ActualEndDate;
        entity.UpdatedBy = _currentUserProvider.Username;
        entity.LastModifiedById = _currentUserProvider.UserId;

        if (!string.Equals(originalStatus, normalizedStatus, StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(normalizedStatus, ProjectPhaseStatuses.InProgress, StringComparison.OrdinalIgnoreCase))
            {
                await ApplyPhasePackageStatusNudgesAsync(entity, phaseCompleted: false);
            }
            else if (string.Equals(normalizedStatus, ProjectPhaseStatuses.Completed, StringComparison.OrdinalIgnoreCase))
            {
                await ApplyPhasePackageStatusNudgesAsync(entity, phaseCompleted: true);
            }
        }

        await _unitOfWork.Repository<ProjectPhase>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return MapToDto(entity);
    }

    public async Task ReorderProjectPhasesAsync(Guid projectId, ReorderProjectPhasesDto dto)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.ManagePlan);
        var phaseRepository = _unitOfWork.Repository<ProjectPhase>();
        var siblings = (await phaseRepository.FindAsync(x =>
            x.ProjectId == projectId
            && x.TenantId == _currentUserProvider.TenantId
            && x.ParentPhaseId == dto.ParentPhaseId))
            .OrderBy(x => x.SortOrder)
            .ToList();

        if (siblings.Count == 0)
        {
            return;
        }

        var requestedIds = dto.OrderedIds.Distinct().ToList();
        if (requestedIds.Count != siblings.Count || siblings.Any(x => !requestedIds.Contains(x.Id)))
        {
            throw new InvalidOperationException("Ordered phase IDs must exactly match the selected phase collection.");
        }

        for (var index = 0; index < requestedIds.Count; index++)
        {
            var phase = siblings.Single(x => x.Id == requestedIds[index]);
            phase.SortOrder = index;
            phase.UpdatedBy = _currentUserProvider.Username;
            phase.LastModifiedById = _currentUserProvider.UserId;
        }

        await phaseRepository.UpdateRangeAsync(siblings);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task DeleteProjectPhaseAsync(Guid phaseId)
    {
        var phase = await GetProjectPhaseEntityAsync(phaseId);
        await RequireProjectAsync(phase.ProjectId, ProjectAccessOperation.ManagePlan);

        var phases = (await GetProjectPhaseEntitiesAsync(phase.ProjectId)).ToList();
        var descendantIds = CollectProjectPhaseDescendantIds(phaseId, phases);
        descendantIds.Add(phaseId);

        var targets = phases.Where(x => descendantIds.Contains(x.Id)).ToList();
        if (targets.Count == 0)
        {
            return;
        }

        await _unitOfWork.Repository<ProjectPhase>().DeleteRangeAsync(targets);
        await _unitOfWork.SaveChangesAsync();
    }

    private async Task ApplyProjectConstructionTemplateAsync(Project project, JsonElement rootElement, UpsertProjectDevelopmentProfileDto? requestedDevelopmentProfile)
    {
        var templateProfile = ParseProjectDevelopmentProfile(rootElement);
        var effectiveProfile = MergeProjectDevelopmentProfile(templateProfile, requestedDevelopmentProfile);

        if (effectiveProfile != null)
        {
            await UpsertDevelopmentProfileEntityAsync(project, effectiveProfile);
        }

        var seededTemplatePhases = await SeedProjectPhasesFromTemplateAsync(project, rootElement);
        if (!seededTemplatePhases && effectiveProfile != null && !await HasProjectPhasesAsync(project.Id))
        {
            await SeedDefaultConstructionPhasesAsync(project.Id);
        }
    }

    private async Task UpsertProjectConstructionFoundationAsync(Project project, UpsertProjectDevelopmentProfileDto dto)
    {
        await UpsertDevelopmentProfileEntityAsync(project, dto);
        if (!await HasProjectPhasesAsync(project.Id))
        {
            await SeedDefaultConstructionPhasesAsync(project.Id);
        }
    }

    private async Task<ProjectDevelopmentProfile> UpsertDevelopmentProfileEntityAsync(Project project, UpsertProjectDevelopmentProfileDto dto)
    {
        var repository = _unitOfWork.Repository<ProjectDevelopmentProfile>();
        var profile = await repository.FirstOrDefaultAsync(x => x.ProjectId == project.Id && x.TenantId == _currentUserProvider.TenantId);
        var previousLandReference = profile?.LandReference;
        dto.LandReference = await ResolveDevelopmentProfileLandReferenceAsync(
            project.Id,
            profile?.LandReference,
            dto.LandReference);
        if (profile == null)
        {
            profile = new ProjectDevelopmentProfile
            {
                TenantId = _currentUserProvider.TenantId,
                ProjectId = project.Id,
                CreatedBy = _currentUserProvider.Username,
                CreatedById = _currentUserProvider.UserId
            };

            ApplyDevelopmentProfile(profile, dto);
            await repository.AddAsync(profile);
        }
        else
        {
            ApplyDevelopmentProfile(profile, dto);
            profile.UpdatedBy = _currentUserProvider.Username;
            profile.LastModifiedById = _currentUserProvider.UserId;
            await repository.UpdateAsync(profile);
        }

        await _unitOfWork.SaveChangesAsync();
        await SynchronizeProjectLandAssetsAsync(
            project,
            previousLandReference,
            profile.LandReference);
        project.DevelopmentProfile = profile;
        return profile;
    }

    private async Task<string?> ResolveDevelopmentProfileLandReferenceAsync(
        Guid projectId,
        string? currentLandReference,
        string? requestedLandReference)
    {
        var normalizedCurrent = TrimOrNull(currentLandReference);
        var normalizedRequested = TrimOrNull(requestedLandReference);
        if (normalizedRequested == null)
        {
            return null;
        }

        if (normalizedCurrent != null
            && string.Equals(normalizedCurrent, normalizedRequested, StringComparison.OrdinalIgnoreCase))
        {
            return normalizedCurrent;
        }

        // Estate integration: only newly selected or changed land references must resolve to ready Estate land-bank assets.
        return await ResolveReadyProjectLandReferenceAsync(
            normalizedRequested,
            projectId);
    }

    private async Task<string?> ResolveReadyProjectLandReferenceAsync(
        string? landReference,
        Guid? excludedProjectId = null)
    {
        var normalizedReference = TrimOrNull(landReference);
        if (normalizedReference == null)
        {
            return null;
        }

        var selection = await RequireReadyProjectLandDemarcationAsync(
            normalizedReference,
            excludedProjectId);
        return selection.LandReference;
    }

    private async Task<ReadyProjectLandSelection> RequireReadyProjectLandDemarcationAsync(
        string landReference,
        Guid? excludedProjectId = null)
    {
        var readyLandAssets = await _unitOfWork.Repository<EstateManagedAsset>().FindAsync(asset =>
            asset.TenantId == _currentUserProvider.TenantId
            && !asset.IsDeleted
            && asset.AssetType == EstateManagedAssetType.Land
            && asset.Status == EstateManagedAssetStatus.LandBank
            && asset.IsReadyForProjectManagement);

        var readyAssetList = readyLandAssets.ToList();
        var readyAssetIds = readyAssetList.Select(asset => asset.Id).ToList();
        var verifiedDemarcations = (await _unitOfWork.Repository<EstateLandDemarcation>().FindAsync(item =>
                item.TenantId == _currentUserProvider.TenantId
                && !item.IsDeleted
                && readyAssetIds.Contains(item.EstateManagedAssetId)
                && item.BoundaryVerified))
            .ToList();
        var assignedLandReferences = (await _unitOfWork.Repository<ProjectDevelopmentProfile>().FindAsync(item =>
                item.TenantId == _currentUserProvider.TenantId
                && !item.IsDeleted
                && item.LandReference != null
                && (!excludedProjectId.HasValue || item.ProjectId != excludedProjectId.Value)))
            .Select(item => item.LandReference!.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        ReadyProjectLandSelection? selection = null;
        foreach (var asset in readyAssetList)
        {
            var assetDemarcations = verifiedDemarcations
                .Where(item => item.EstateManagedAssetId == asset.Id)
                .OrderBy(item => item.DemarcationNumber)
                .ToList();
            foreach (var demarcation in assetDemarcations)
            {
                var demarcationReference = EstateLandDemarcationReference.Build(
                    asset.AssetCode,
                    demarcation.DemarcationNumber);
                var isAssigned = assignedLandReferences.Contains(demarcationReference)
                    || (assetDemarcations.Count == 1
                        && new[] { asset.AssetCode, asset.ProjectCode, asset.Name, asset.Id.ToString() }
                            .Any(reference => !string.IsNullOrWhiteSpace(reference)
                                && assignedLandReferences.Contains(reference.Trim())));
                if (isAssigned)
                {
                    continue;
                }

                var matchesDemarcation = MatchesLandReference(demarcationReference, landReference)
                    || MatchesLandReference(demarcation.Id.ToString(), landReference);
                var matchesSingleWholeParcel = assetDemarcations.Count == 1
                    && (MatchesLandReference(asset.AssetCode, landReference)
                        || MatchesLandReference(asset.ProjectCode, landReference)
                        || MatchesLandReference(asset.Name, landReference)
                        || MatchesLandReference(asset.Id.ToString(), landReference));
                if (matchesDemarcation || matchesSingleWholeParcel)
                {
                    selection = new ReadyProjectLandSelection(asset, demarcation, demarcationReference);
                    break;
                }
            }

            if (selection != null)
            {
                break;
            }
        }

        if (selection == null)
        {
            throw new InvalidOperationException("Project land reference must be a verified, unused demarcated portion marked ready for project management.");
        }

        return selection;
    }

    private async Task<(int ActiveCount, bool HasUnusedPortion, bool AllPortionsVerified)> GetProjectLandAvailabilityAsync(
        Guid assetId)
    {
        var demarcations = (await _unitOfWork.Repository<EstateLandDemarcation>().FindAsync(item =>
                item.TenantId == _currentUserProvider.TenantId
                && !item.IsDeleted
                && item.EstateManagedAssetId == assetId))
            .ToList();
        if (demarcations.Count == 0)
        {
            return (0, false, false);
        }

        var asset = await _unitOfWork.Repository<EstateManagedAsset>().FirstOrDefaultAsync(item =>
            item.Id == assetId
            && item.TenantId == _currentUserProvider.TenantId
            && !item.IsDeleted);
        if (asset == null)
        {
            return (0, false, false);
        }

        var assignedLandReferences = (await _unitOfWork.Repository<ProjectDevelopmentProfile>().FindAsync(item =>
                item.TenantId == _currentUserProvider.TenantId
                && !item.IsDeleted
                && item.LandReference != null))
            .Select(item => item.LandReference!.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var hasLegacyWholeParcelAssignment = demarcations.Count == 1
            && new[] { asset.AssetCode, asset.ProjectCode, asset.Name, asset.Id.ToString() }
                .Any(reference => !string.IsNullOrWhiteSpace(reference)
                    && assignedLandReferences.Contains(reference.Trim()));
        var hasUnusedPortion = demarcations.Any(item =>
            !assignedLandReferences.Contains(
                EstateLandDemarcationReference.Build(asset.AssetCode, item.DemarcationNumber))
            && !hasLegacyWholeParcelAssignment);
        return (
            demarcations.Count,
            hasUnusedPortion,
            demarcations.All(item => item.BoundaryVerified));
    }

    private async Task<ReadyProjectLandSelection?> FindProjectLandSelectionAsync(string? landReference)
    {
        var normalizedReference = TrimOrNull(landReference);
        if (normalizedReference == null)
        {
            return null;
        }

        var assets = (await _unitOfWork.Repository<EstateManagedAsset>().FindAsync(asset =>
                asset.TenantId == _currentUserProvider.TenantId
                && !asset.IsDeleted
                && asset.AssetType == EstateManagedAssetType.Land))
            .ToList();
        var assetIds = assets.Select(asset => asset.Id).ToList();
        var demarcations = (await _unitOfWork.Repository<EstateLandDemarcation>().FindAsync(item =>
                item.TenantId == _currentUserProvider.TenantId
                && !item.IsDeleted
                && item.BoundaryVerified
                && assetIds.Contains(item.EstateManagedAssetId)))
            .ToList();

        foreach (var asset in assets)
        {
            var assetDemarcations = demarcations
                .Where(item => item.EstateManagedAssetId == asset.Id)
                .OrderBy(item => item.DemarcationNumber)
                .ToList();
            foreach (var demarcation in assetDemarcations)
            {
                var demarcationReference = EstateLandDemarcationReference.Build(
                    asset.AssetCode,
                    demarcation.DemarcationNumber);
                var matchesDemarcation = MatchesLandReference(demarcationReference, normalizedReference)
                    || MatchesLandReference(demarcation.Id.ToString(), normalizedReference);
                var matchesLegacyWholeParcel = assetDemarcations.Count == 1
                    && (MatchesLandReference(asset.AssetCode, normalizedReference)
                        || MatchesLandReference(asset.ProjectCode, normalizedReference)
                        || MatchesLandReference(asset.Name, normalizedReference)
                        || MatchesLandReference(asset.Id.ToString(), normalizedReference));
                if (matchesDemarcation || matchesLegacyWholeParcel)
                {
                    return new ReadyProjectLandSelection(asset, demarcation, demarcationReference);
                }
            }
        }

        return null;
    }

    private async Task SynchronizeProjectLandAssetsAsync(
        Project project,
        string? previousLandReference,
        string? currentLandReference)
    {
        var previousSelection = await FindProjectLandSelectionAsync(previousLandReference);
        var currentSelection = await FindProjectLandSelectionAsync(currentLandReference);
        var impactedAssets = new[] { previousSelection?.Asset, currentSelection?.Asset }
            .Where(asset => asset != null)
            .Cast<EstateManagedAsset>()
            .GroupBy(asset => asset.Id)
            .Select(group => group.First())
            .ToList();

        foreach (var asset in impactedAssets)
        {
            var availability = await GetProjectLandAvailabilityAsync(asset.Id);
            if (availability.HasUnusedPortion)
            {
                asset.Status = EstateManagedAssetStatus.LandBank;
                asset.IsReadyForProjectManagement = asset.IsReadyForProjectManagement
                    && availability.AllPortionsVerified
                    && !asset.IsPublishedToExternalPortal;
                if (asset.ProjectId == project.Id)
                {
                    asset.ProjectId = null;
                    asset.ProjectTitle = null;
                }
            }
            else
            {
                asset.Status = EstateManagedAssetStatus.UnderDevelopment;
                asset.IsReadyForProjectManagement = false;
                if (availability.ActiveCount == 1
                    && availability.AllPortionsVerified
                    && currentSelection?.Asset.Id == asset.Id)
                {
                    asset.ProjectId = project.Id;
                    asset.ProjectTitle = project.Title;
                }
            }

            asset.UpdatedAt = DateTime.UtcNow;
            asset.UpdatedBy = _currentUserProvider.Username;
            asset.LastModifiedById = _currentUserProvider.UserId;
            await _unitOfWork.Repository<EstateManagedAsset>().UpdateAsync(asset);
        }

        if (impactedAssets.Count > 0)
        {
            await _unitOfWork.SaveChangesAsync();
        }
    }

    private static bool MatchesLandReference(string? candidate, string reference)
        => !string.IsNullOrWhiteSpace(candidate)
            && string.Equals(candidate.Trim(), reference, StringComparison.OrdinalIgnoreCase);

    private sealed record ReadyProjectLandSelection(
        EstateManagedAsset Asset,
        EstateLandDemarcation Demarcation,
        string LandReference);

    private async Task<bool> SeedProjectPhasesFromTemplateAsync(Project project, JsonElement rootElement)
    {
        if (await HasProjectPhasesAsync(project.Id))
        {
            return false;
        }

        if (!TryGetProjectPhaseTemplateArray(rootElement, out var phaseTemplates))
        {
            return false;
        }

        var repository = _unitOfWork.Repository<ProjectPhase>();
        var seededAny = false;
        var sortOrder = 0;
        foreach (var phaseTemplate in phaseTemplates.EnumerateArray())
        {
            await AddProjectPhaseFromTemplateAsync(repository, project.Id, phaseTemplate, null, sortOrder++);
            seededAny = true;
        }

        if (seededAny)
        {
            await _unitOfWork.SaveChangesAsync();
        }

        return seededAny;
    }

    private async Task AddProjectPhaseFromTemplateAsync(
        IGenericRepository<ProjectPhase> repository,
        Guid projectId,
        JsonElement templateElement,
        Guid? parentPhaseId,
        int sortOrder)
    {
        var phase = ParseProjectPhaseTemplate(projectId, templateElement, parentPhaseId, sortOrder);
        if (phase == null)
        {
            return;
        }

        await repository.AddAsync(phase);

        if (templateElement.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (!TryGetChildProjectPhaseArray(templateElement, out var children))
        {
            return;
        }

        var childOrder = 0;
        foreach (var child in children.EnumerateArray())
        {
            await AddProjectPhaseFromTemplateAsync(repository, projectId, child, phase.Id, childOrder++);
        }
    }

    private async Task SeedDefaultConstructionPhasesAsync(Guid projectId)
    {
        if (await HasProjectPhasesAsync(projectId))
        {
            return;
        }

        var project = await _projectRepository.GetByIdAsync(projectId);
        if (project != null && await SeedConfiguredConstructionPhasesAsync(project))
        {
            return;
        }

        var phases = DefaultConstructionPhaseBlueprints
            .Select((phase, index) => new ProjectPhase
            {
                TenantId = _currentUserProvider.TenantId,
                ProjectId = projectId,
                Code = phase.Code,
                Name = phase.Name,
                Status = ProjectPhaseStatuses.NotStarted,
                SortOrder = index,
                IsOptional = false,
                IsStageGateRequired = phase.IsStageGateRequired,
                IsTemplateSeeded = true,
                CreatedBy = _currentUserProvider.Username,
                CreatedById = _currentUserProvider.UserId
            })
            .ToList();

        await _unitOfWork.Repository<ProjectPhase>().AddRangeAsync(phases);
        await _unitOfWork.SaveChangesAsync();
    }

    private async Task<ProjectPhase> GetProjectPhaseEntityAsync(Guid phaseId)
        => await _unitOfWork.Repository<ProjectPhase>()
            .GetByIdAsync(phaseId)
            ?? throw new InvalidOperationException($"Project phase with ID {phaseId} not found");

    private async Task<List<ProjectPhase>> GetProjectPhaseEntitiesAsync(Guid projectId)
        => (await _unitOfWork.Repository<ProjectPhase>().FindAsync(x =>
            x.ProjectId == projectId
            && x.TenantId == _currentUserProvider.TenantId))
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToList();

    private async Task<bool> HasProjectPhasesAsync(Guid projectId)
        => await _unitOfWork.Repository<ProjectPhase>().ExistsAsync(x =>
            x.ProjectId == projectId
            && x.TenantId == _currentUserProvider.TenantId);

    private static HashSet<Guid> CollectProjectPhaseDescendantIds(Guid phaseId, IReadOnlyCollection<ProjectPhase> phases)
    {
        var children = phases.Where(x => x.ParentPhaseId == phaseId).Select(x => x.Id).ToList();
        var ids = new HashSet<Guid>(children);
        foreach (var childId in children)
        {
            ids.UnionWith(CollectProjectPhaseDescendantIds(childId, phases));
        }

        return ids;
    }

    private static void ApplyDevelopmentProfile(ProjectDevelopmentProfile entity, UpsertProjectDevelopmentProfileDto dto)
    {
        entity.DeliveryStructure = NormalizeDeliveryStructure(dto.DeliveryStructure);
        entity.DevelopmentType = TrimOrNull(dto.DevelopmentType);
        entity.SiteName = TrimOrNull(dto.SiteName);
        entity.SiteAddress = TrimOrNull(dto.SiteAddress);
        entity.LandReference = TrimOrNull(dto.LandReference);
        entity.ProcurementRoute = TrimOrNull(dto.ProcurementRoute);
        entity.ContractStrategy = TrimOrNull(dto.ContractStrategy);
        entity.ConsultantTeam = TrimOrNull(dto.ConsultantTeam);
        entity.FundingArrangement = TrimOrNull(dto.FundingArrangement);
        entity.HandoverStrategy = TrimOrNull(dto.HandoverStrategy);
        entity.Notes = TrimOrNull(dto.Notes);
    }

    private static UpsertProjectDevelopmentProfileDto? MergeProjectDevelopmentProfile(
        UpsertProjectDevelopmentProfileDto? templateProfile,
        UpsertProjectDevelopmentProfileDto? requestedProfile)
    {
        if (templateProfile == null && requestedProfile == null)
        {
            return null;
        }

        return new UpsertProjectDevelopmentProfileDto
        {
            DeliveryStructure = NormalizeDeliveryStructure(requestedProfile?.DeliveryStructure ?? templateProfile?.DeliveryStructure),
            DevelopmentType = requestedProfile?.DevelopmentType ?? templateProfile?.DevelopmentType,
            SiteName = requestedProfile?.SiteName ?? templateProfile?.SiteName,
            SiteAddress = requestedProfile?.SiteAddress ?? templateProfile?.SiteAddress,
            LandReference = requestedProfile?.LandReference ?? templateProfile?.LandReference,
            ProcurementRoute = requestedProfile?.ProcurementRoute ?? templateProfile?.ProcurementRoute,
            ContractStrategy = requestedProfile?.ContractStrategy ?? templateProfile?.ContractStrategy,
            ConsultantTeam = requestedProfile?.ConsultantTeam ?? templateProfile?.ConsultantTeam,
            FundingArrangement = requestedProfile?.FundingArrangement ?? templateProfile?.FundingArrangement,
            HandoverStrategy = requestedProfile?.HandoverStrategy ?? templateProfile?.HandoverStrategy,
            Notes = requestedProfile?.Notes ?? templateProfile?.Notes
        };
    }

    private static UpsertProjectDevelopmentProfileDto? ParseProjectDevelopmentProfile(JsonElement rootElement)
    {
        if (!TryGetNamedObject(rootElement, "developmentProfile", out var profileElement)
            && !TryGetNamedObject(rootElement, "constructionProfile", out profileElement))
        {
            return null;
        }

        return new UpsertProjectDevelopmentProfileDto
        {
            DeliveryStructure = NormalizeDeliveryStructure(ReadString(profileElement, "deliveryStructure")),
            DevelopmentType = ReadString(profileElement, "developmentType"),
            SiteName = ReadString(profileElement, "siteName"),
            SiteAddress = ReadString(profileElement, "siteAddress"),
            LandReference = ReadString(profileElement, "landReference"),
            ProcurementRoute = ReadString(profileElement, "procurementRoute"),
            ContractStrategy = ReadString(profileElement, "contractStrategy"),
            ConsultantTeam = ReadString(profileElement, "consultantTeam"),
            FundingArrangement = ReadString(profileElement, "fundingArrangement"),
            HandoverStrategy = ReadString(profileElement, "handoverStrategy"),
            Notes = ReadString(profileElement, "notes")
        };
    }

    private ProjectPhase? ParseProjectPhaseTemplate(Guid projectId, JsonElement templateElement, Guid? parentPhaseId, int sortOrder)
    {
        string? name;
        string? code = null;
        string? description = null;
        var status = ProjectPhaseStatuses.NotStarted;
        var isOptional = false;
        var isStageGateRequired = false;
        DateTime? plannedStartDate = null;
        DateTime? plannedEndDate = null;
        DateTime? actualStartDate = null;
        DateTime? actualEndDate = null;
        decimal completionWeightPercent = 0m;
        var explicitSortOrder = sortOrder;

        if (templateElement.ValueKind == JsonValueKind.String)
        {
            name = templateElement.GetString();
        }
        else if (templateElement.ValueKind == JsonValueKind.Object)
        {
            name = ReadString(templateElement, "name") ?? ReadString(templateElement, "title");
            code = ReadString(templateElement, "code");
            description = ReadString(templateElement, "description");
            status = NormalizeProjectPhaseStatus(ReadString(templateElement, "status"));
            isOptional = ReadBool(templateElement, "isOptional");
            isStageGateRequired = ReadBool(templateElement, "isStageGateRequired") || ReadBool(templateElement, "stageGateRequired");
            plannedStartDate = ReadDateTime(templateElement, "plannedStartDate");
            plannedEndDate = ReadDateTime(templateElement, "plannedEndDate");
            actualStartDate = ReadDateTime(templateElement, "actualStartDate");
            actualEndDate = ReadDateTime(templateElement, "actualEndDate");
            completionWeightPercent = ReadDecimal(templateElement, "completionWeightPercent");
            if (TryReadInt(templateElement, "sortOrder", out var configuredSortOrder))
            {
                explicitSortOrder = configuredSortOrder;
            }
        }
        else
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return new ProjectPhase
        {
            TenantId = _currentUserProvider.TenantId,
            ProjectId = projectId,
            ParentPhaseId = parentPhaseId,
            Code = TrimOrNull(code),
            Name = name.Trim(),
            Description = TrimOrNull(description),
            Status = status,
            SortOrder = explicitSortOrder,
            IsOptional = isOptional,
            IsStageGateRequired = isStageGateRequired,
            IsTemplateSeeded = true,
            CompletionWeightPercent = completionWeightPercent,
            PlannedStartDate = plannedStartDate,
            PlannedEndDate = plannedEndDate,
            ActualStartDate = actualStartDate,
            ActualEndDate = actualEndDate,
            CreatedBy = _currentUserProvider.Username,
            CreatedById = _currentUserProvider.UserId
        };
    }

    private static bool TryGetProjectPhaseTemplateArray(JsonElement rootElement, out JsonElement phasesElement)
    {
        if (rootElement.TryGetProperty("projectPhases", out phasesElement) && phasesElement.ValueKind == JsonValueKind.Array)
        {
            return true;
        }

        if (rootElement.TryGetProperty("phases", out phasesElement) && phasesElement.ValueKind == JsonValueKind.Array)
        {
            return true;
        }

        phasesElement = default;
        return false;
    }

    private static bool TryGetChildProjectPhaseArray(JsonElement rootElement, out JsonElement phasesElement)
    {
        if (rootElement.TryGetProperty("children", out phasesElement) && phasesElement.ValueKind == JsonValueKind.Array)
        {
            return true;
        }

        if (rootElement.TryGetProperty("subphases", out phasesElement) && phasesElement.ValueKind == JsonValueKind.Array)
        {
            return true;
        }

        if (rootElement.TryGetProperty("phases", out phasesElement) && phasesElement.ValueKind == JsonValueKind.Array)
        {
            return true;
        }

        phasesElement = default;
        return false;
    }

    private static bool TryGetNamedObject(JsonElement rootElement, string propertyName, out JsonElement element)
    {
        if (rootElement.TryGetProperty(propertyName, out element) && element.ValueKind == JsonValueKind.Object)
        {
            return true;
        }

        element = default;
        return false;
    }

    private static string NormalizeDeliveryStructure(string? value)
    {
        var normalized = value?.Trim();
        return normalized switch
        {
            ProjectDeliveryStructures.SingleUnit => ProjectDeliveryStructures.SingleUnit,
            ProjectDeliveryStructures.MultiUnit => ProjectDeliveryStructures.MultiUnit,
            _ => ProjectDeliveryStructures.WholeDevelopment
        };
    }

    private static string NormalizeProjectPhaseStatus(string? value)
    {
        var normalized = value?.Trim();
        return normalized switch
        {
            ProjectPhaseStatuses.InProgress => ProjectPhaseStatuses.InProgress,
            ProjectPhaseStatuses.Blocked => ProjectPhaseStatuses.Blocked,
            ProjectPhaseStatuses.Completed => ProjectPhaseStatuses.Completed,
            ProjectPhaseStatuses.Waived => ProjectPhaseStatuses.Waived,
            ProjectPhaseStatuses.Cancelled => ProjectPhaseStatuses.Cancelled,
            _ => ProjectPhaseStatuses.NotStarted
        };
    }

    private static string? ReadString(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()?.Trim()
            : null;

    private static bool ReadBool(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var property)
            && property.ValueKind is JsonValueKind.True or JsonValueKind.False
            && property.GetBoolean();

    private static DateTime? ReadDateTime(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String
            && property.TryGetDateTime(out var parsed)
                ? parsed
                : null;

    private static decimal ReadDecimal(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.Number
            && property.TryGetDecimal(out var parsed)
                ? decimal.Round(parsed, 2)
                : 0m;

    private static bool TryReadInt(JsonElement element, string propertyName, out int value)
    {
        if (element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out value))
        {
            return true;
        }

        value = default;
        return false;
    }

    private static string? TrimOrNull(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private static ProjectDevelopmentProfileDto MapToDto(ProjectDevelopmentProfile entity) => new()
    {
        Id = entity.Id,
        ProjectId = entity.ProjectId,
        DeliveryStructure = entity.DeliveryStructure,
        DevelopmentType = entity.DevelopmentType,
        SiteName = entity.SiteName,
        SiteAddress = entity.SiteAddress,
        LandReference = entity.LandReference,
        ProcurementRoute = entity.ProcurementRoute,
        ContractStrategy = entity.ContractStrategy,
        ConsultantTeam = entity.ConsultantTeam,
        FundingArrangement = entity.FundingArrangement,
        HandoverStrategy = entity.HandoverStrategy,
        Notes = entity.Notes
    };

    private static ProjectPhaseDto MapToDto(ProjectPhase entity) => new()
    {
        Id = entity.Id,
        ProjectId = entity.ProjectId,
        ParentPhaseId = entity.ParentPhaseId,
        Code = entity.Code,
        Name = entity.Name,
        Description = entity.Description,
        Status = entity.Status,
        SortOrder = entity.SortOrder,
        IsOptional = entity.IsOptional,
        IsStageGateRequired = entity.IsStageGateRequired,
        IsTemplateSeeded = entity.IsTemplateSeeded,
        CompletionWeightPercent = entity.CompletionWeightPercent,
        PlannedStartDate = entity.PlannedStartDate,
        PlannedEndDate = entity.PlannedEndDate,
        ActualStartDate = entity.ActualStartDate,
        ActualEndDate = entity.ActualEndDate
    };

    private static decimal NormalizeCompletionWeightPercent(decimal? completionWeightPercent, string label)
    {
        var resolvedWeight = decimal.Round(completionWeightPercent ?? 0m, 2);
        if (resolvedWeight < 0m || resolvedWeight > 100m)
        {
            throw new InvalidOperationException($"The {label} completion weight must be between 0 and 100.");
        }

        return resolvedWeight;
    }

    private static List<ProjectPhaseDto> MapToPhaseTree(IEnumerable<ProjectPhase> entities)
    {
        var ordered = entities
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToList();

        if (ordered.Count == 0)
        {
            return [];
        }

        var lookup = ordered.ToDictionary(x => x.Id, MapToDto);
        var roots = new List<ProjectPhaseDto>();

        foreach (var entity in ordered)
        {
            var dto = lookup[entity.Id];
            if (entity.ParentPhaseId.HasValue && lookup.TryGetValue(entity.ParentPhaseId.Value, out var parent))
            {
                parent.Children.Add(dto);
            }
            else
            {
                roots.Add(dto);
            }
        }

        SortPhaseChildren(roots);
        return roots;
    }

    private static void SortPhaseChildren(List<ProjectPhaseDto> phases)
    {
        phases.Sort((left, right) =>
        {
            var sortComparison = left.SortOrder.CompareTo(right.SortOrder);
            return sortComparison != 0
                ? sortComparison
                : string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
        });

        foreach (var phase in phases)
        {
            if (phase.Children.Count > 0)
            {
                SortPhaseChildren(phase.Children);
            }
        }
    }
}
