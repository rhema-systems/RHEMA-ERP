using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Entities.Projects;

namespace ErpSystem.Core.Services.Projects;

public partial class ProjectService
{
    public async Task<IEnumerable<ProjectBuildingDto>> GetProjectBuildingsAsync(Guid projectId)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.View);
        return MapProjectBuildings((await GetProjectBuildingEntitiesAsync(projectId)).ToList());
    }

    public async Task<ProjectBuildingDto> AddProjectBuildingAsync(Guid projectId, CreateProjectBuildingDto dto)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.ManageFinancials);
        var siblings = (await GetProjectBuildingEntitiesAsync(projectId)).ToList();
        var entity = new ProjectBuilding
        {
            TenantId = _currentUserProvider.TenantId,
            ProjectId = projectId,
            Code = TrimOrNull(dto.Code),
            Name = dto.Name.Trim(),
            SortOrder = dto.SortOrder ?? (siblings.Count == 0 ? 0 : siblings.Max(x => x.SortOrder) + 1),
            Notes = TrimOrNull(dto.Notes),
            CreatedBy = _currentUserProvider.Username,
            CreatedById = _currentUserProvider.UserId,
        };

        await _unitOfWork.Repository<ProjectBuilding>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return await GetProjectBuildingDtoAsync(projectId, entity.Id);
    }

    public async Task<ProjectBuildingDto> UpdateProjectBuildingAsync(Guid buildingId, UpdateProjectBuildingDto dto)
    {
        var entity = await GetProjectBuildingEntityAsync(buildingId);
        await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageFinancials);

        entity.Code = TrimOrNull(dto.Code);
        entity.Name = dto.Name.Trim();
        entity.SortOrder = dto.SortOrder ?? entity.SortOrder;
        entity.Notes = TrimOrNull(dto.Notes);
        entity.UpdatedBy = _currentUserProvider.Username;
        entity.LastModifiedById = _currentUserProvider.UserId;

        await _unitOfWork.Repository<ProjectBuilding>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return await GetProjectBuildingDtoAsync(entity.ProjectId, entity.Id);
    }

    public async Task DeleteProjectBuildingAsync(Guid buildingId)
    {
        var entity = await GetProjectBuildingEntityAsync(buildingId);
        await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageFinancials);
        await _unitOfWork.Repository<ProjectBuilding>().DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<IEnumerable<ProjectFloorDto>> GetProjectFloorsAsync(Guid projectId)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.View);
        return await MapProjectFloorsAsync((await GetProjectFloorEntitiesAsync(projectId)).ToList());
    }

    public async Task<ProjectFloorDto> AddProjectFloorAsync(Guid projectId, CreateProjectFloorDto dto)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.ManageFinancials);
        var building = await ValidateProjectBuildingAsync(projectId, dto.ProjectBuildingId);
        var siblings = (await GetProjectFloorEntitiesAsync(projectId)).ToList();

        var entity = new ProjectFloor
        {
            TenantId = _currentUserProvider.TenantId,
            ProjectId = projectId,
            ProjectBuildingId = building?.Id,
            Code = TrimOrNull(dto.Code),
            Name = dto.Name.Trim(),
            LevelNumber = dto.LevelNumber,
            SortOrder = dto.SortOrder ?? (siblings.Count == 0 ? 0 : siblings.Max(x => x.SortOrder) + 1),
            Notes = TrimOrNull(dto.Notes),
            CreatedBy = _currentUserProvider.Username,
            CreatedById = _currentUserProvider.UserId,
        };

        await _unitOfWork.Repository<ProjectFloor>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return await GetProjectFloorDtoAsync(projectId, entity.Id);
    }

    public async Task<ProjectFloorDto> UpdateProjectFloorAsync(Guid floorId, UpdateProjectFloorDto dto)
    {
        var entity = await GetProjectFloorEntityAsync(floorId);
        await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageFinancials);
        var building = await ValidateProjectBuildingAsync(entity.ProjectId, dto.ProjectBuildingId);

        entity.ProjectBuildingId = building?.Id;
        entity.Code = TrimOrNull(dto.Code);
        entity.Name = dto.Name.Trim();
        entity.LevelNumber = dto.LevelNumber;
        entity.SortOrder = dto.SortOrder ?? entity.SortOrder;
        entity.Notes = TrimOrNull(dto.Notes);
        entity.UpdatedBy = _currentUserProvider.Username;
        entity.LastModifiedById = _currentUserProvider.UserId;

        await _unitOfWork.Repository<ProjectFloor>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return await GetProjectFloorDtoAsync(entity.ProjectId, entity.Id);
    }

    public async Task DeleteProjectFloorAsync(Guid floorId)
    {
        var entity = await GetProjectFloorEntityAsync(floorId);
        await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageFinancials);
        await _unitOfWork.Repository<ProjectFloor>().DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<IEnumerable<ProjectUnitReleaseBatchDto>> GetProjectUnitReleaseBatchesAsync(Guid projectId)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.View);
        return await MapProjectUnitReleaseBatchesAsync((await GetProjectUnitReleaseBatchEntitiesAsync(projectId)).ToList());
    }

    public async Task<ProjectUnitReleaseBatchDto> AddProjectUnitReleaseBatchAsync(Guid projectId, CreateProjectUnitReleaseBatchDto dto)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.ManageFinancials);
        var (building, floor) = await ResolveProjectHierarchyAsync(projectId, dto.ProjectBuildingId, dto.ProjectFloorId);
        var siblings = (await GetProjectUnitReleaseBatchEntitiesAsync(projectId)).ToList();
        var entity = new ProjectUnitReleaseBatch
        {
            TenantId = _currentUserProvider.TenantId,
            ProjectId = projectId,
            ProjectBuildingId = building?.Id,
            ProjectFloorId = floor?.Id,
            Code = TrimOrNull(dto.Code),
            Name = dto.Name.Trim(),
            Status = NormalizeProjectUnitReleaseBatchStatus(dto.Status),
            PlannedReleaseDate = dto.PlannedReleaseDate,
            ActualReleaseDate = dto.ActualReleaseDate,
            SortOrder = dto.SortOrder ?? (siblings.Count == 0 ? 0 : siblings.Max(x => x.SortOrder) + 1),
            Notes = TrimOrNull(dto.Notes),
            CreatedBy = _currentUserProvider.Username,
            CreatedById = _currentUserProvider.UserId,
        };

        await _unitOfWork.Repository<ProjectUnitReleaseBatch>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return await GetProjectUnitReleaseBatchDtoAsync(projectId, entity.Id);
    }

    public async Task<ProjectUnitReleaseBatchDto> UpdateProjectUnitReleaseBatchAsync(Guid unitReleaseBatchId, UpdateProjectUnitReleaseBatchDto dto)
    {
        var entity = await GetProjectUnitReleaseBatchEntityAsync(unitReleaseBatchId);
        await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageFinancials);
        var (building, floor) = await ResolveProjectHierarchyAsync(entity.ProjectId, dto.ProjectBuildingId, dto.ProjectFloorId);

        entity.ProjectBuildingId = building?.Id;
        entity.ProjectFloorId = floor?.Id;
        entity.Code = TrimOrNull(dto.Code);
        entity.Name = dto.Name.Trim();
        entity.Status = NormalizeProjectUnitReleaseBatchStatus(dto.Status);
        entity.PlannedReleaseDate = dto.PlannedReleaseDate;
        entity.ActualReleaseDate = dto.ActualReleaseDate;
        entity.SortOrder = dto.SortOrder ?? entity.SortOrder;
        entity.Notes = TrimOrNull(dto.Notes);
        entity.UpdatedBy = _currentUserProvider.Username;
        entity.LastModifiedById = _currentUserProvider.UserId;

        await _unitOfWork.Repository<ProjectUnitReleaseBatch>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return await GetProjectUnitReleaseBatchDtoAsync(entity.ProjectId, entity.Id);
    }

    public async Task DeleteProjectUnitReleaseBatchAsync(Guid unitReleaseBatchId)
    {
        var entity = await GetProjectUnitReleaseBatchEntityAsync(unitReleaseBatchId);
        await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageFinancials);
        await _unitOfWork.Repository<ProjectUnitReleaseBatch>().DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<IEnumerable<ProjectUnitHandoverBatchDto>> GetProjectUnitHandoverBatchesAsync(Guid projectId)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.View);
        return await MapProjectUnitHandoverBatchesAsync((await GetProjectUnitHandoverBatchEntitiesAsync(projectId)).ToList());
    }

    public async Task<ProjectUnitHandoverBatchDto> AddProjectUnitHandoverBatchAsync(Guid projectId, CreateProjectUnitHandoverBatchDto dto)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.ManageGovernance);
        var (building, floor) = await ResolveProjectHierarchyAsync(projectId, dto.ProjectBuildingId, dto.ProjectFloorId);
        var siblings = (await GetProjectUnitHandoverBatchEntitiesAsync(projectId)).ToList();
        var entity = new ProjectUnitHandoverBatch
        {
            TenantId = _currentUserProvider.TenantId,
            ProjectId = projectId,
            ProjectBuildingId = building?.Id,
            ProjectFloorId = floor?.Id,
            Code = TrimOrNull(dto.Code),
            Name = dto.Name.Trim(),
            Status = NormalizeProjectUnitHandoverBatchStatus(dto.Status),
            PlannedHandoverDate = dto.PlannedHandoverDate,
            ActualHandoverDate = dto.ActualHandoverDate,
            SortOrder = dto.SortOrder ?? (siblings.Count == 0 ? 0 : siblings.Max(x => x.SortOrder) + 1),
            Notes = TrimOrNull(dto.Notes),
            CreatedBy = _currentUserProvider.Username,
            CreatedById = _currentUserProvider.UserId,
        };

        await _unitOfWork.Repository<ProjectUnitHandoverBatch>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return await GetProjectUnitHandoverBatchDtoAsync(projectId, entity.Id);
    }

    public async Task<ProjectUnitHandoverBatchDto> UpdateProjectUnitHandoverBatchAsync(Guid unitHandoverBatchId, UpdateProjectUnitHandoverBatchDto dto)
    {
        var entity = await GetProjectUnitHandoverBatchEntityAsync(unitHandoverBatchId);
        await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageGovernance);
        var (building, floor) = await ResolveProjectHierarchyAsync(entity.ProjectId, dto.ProjectBuildingId, dto.ProjectFloorId);

        entity.ProjectBuildingId = building?.Id;
        entity.ProjectFloorId = floor?.Id;
        entity.Code = TrimOrNull(dto.Code);
        entity.Name = dto.Name.Trim();
        entity.Status = NormalizeProjectUnitHandoverBatchStatus(dto.Status);
        entity.PlannedHandoverDate = dto.PlannedHandoverDate;
        entity.ActualHandoverDate = dto.ActualHandoverDate;
        entity.SortOrder = dto.SortOrder ?? entity.SortOrder;
        entity.Notes = TrimOrNull(dto.Notes);
        entity.UpdatedBy = _currentUserProvider.Username;
        entity.LastModifiedById = _currentUserProvider.UserId;

        await _unitOfWork.Repository<ProjectUnitHandoverBatch>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return await GetProjectUnitHandoverBatchDtoAsync(entity.ProjectId, entity.Id);
    }

    public async Task DeleteProjectUnitHandoverBatchAsync(Guid unitHandoverBatchId)
    {
        var entity = await GetProjectUnitHandoverBatchEntityAsync(unitHandoverBatchId);
        await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageGovernance);
        await _unitOfWork.Repository<ProjectUnitHandoverBatch>().DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    private async Task<(ProjectBuilding? Building, ProjectFloor? Floor)> ResolveProjectHierarchyAsync(Guid projectId, Guid? buildingId, Guid? floorId)
    {
        var building = await ValidateProjectBuildingAsync(projectId, buildingId);
        var floor = await ValidateProjectFloorAsync(projectId, floorId, building?.Id);
        if (building == null && floor?.ProjectBuildingId.HasValue == true)
        {
            building = await GetProjectBuildingEntityAsync(floor.ProjectBuildingId.Value);
        }

        return (building, floor);
    }

    private async Task<ProjectBuilding?> ValidateProjectBuildingAsync(Guid projectId, Guid? buildingId)
    {
        if (!buildingId.HasValue)
        {
            return null;
        }

        var building = await GetProjectBuildingEntityAsync(buildingId.Value);
        if (building.ProjectId != projectId)
        {
            throw new InvalidOperationException("The selected building does not belong to this project.");
        }

        return building;
    }

    private async Task<ProjectFloor?> ValidateProjectFloorAsync(Guid projectId, Guid? floorId, Guid? buildingId = null)
    {
        if (!floorId.HasValue)
        {
            return null;
        }

        var floor = await GetProjectFloorEntityAsync(floorId.Value);
        if (floor.ProjectId != projectId)
        {
            throw new InvalidOperationException("The selected floor does not belong to this project.");
        }

        if (buildingId.HasValue && floor.ProjectBuildingId != buildingId.Value)
        {
            throw new InvalidOperationException("The selected floor does not belong to the selected building.");
        }

        return floor;
    }

    private async Task<ProjectUnitReleaseBatch?> ValidateProjectUnitReleaseBatchAsync(Guid projectId, Guid? batchId, Guid? buildingId = null, Guid? floorId = null)
    {
        if (!batchId.HasValue)
        {
            return null;
        }

        var batch = await GetProjectUnitReleaseBatchEntityAsync(batchId.Value);
        if (batch.ProjectId != projectId)
        {
            throw new InvalidOperationException("The selected release batch does not belong to this project.");
        }

        if (batch.ProjectBuildingId.HasValue && batch.ProjectBuildingId != buildingId)
        {
            throw new InvalidOperationException("The selected release batch does not align with the unit building.");
        }

        if (batch.ProjectFloorId.HasValue && batch.ProjectFloorId != floorId)
        {
            throw new InvalidOperationException("The selected release batch does not align with the unit floor.");
        }

        return batch;
    }

    private async Task<ProjectUnitHandoverBatch?> ValidateProjectUnitHandoverBatchAsync(Guid projectId, Guid? batchId, Guid? unitId = null)
    {
        if (!batchId.HasValue)
        {
            return null;
        }

        var batch = await GetProjectUnitHandoverBatchEntityAsync(batchId.Value);
        if (batch.ProjectId != projectId)
        {
            throw new InvalidOperationException("The selected handover batch does not belong to this project.");
        }

        if (!unitId.HasValue)
        {
            return batch;
        }

        var unit = await GetProjectUnitEntityAsync(unitId.Value);
        if (batch.ProjectBuildingId.HasValue && unit.ProjectBuildingId != batch.ProjectBuildingId)
        {
            throw new InvalidOperationException("The selected handover batch does not align with the unit building.");
        }

        if (batch.ProjectFloorId.HasValue && unit.ProjectFloorId != batch.ProjectFloorId)
        {
            throw new InvalidOperationException("The selected handover batch does not align with the unit floor.");
        }

        return batch;
    }

    private async Task<List<ProjectBuilding>> GetProjectBuildingEntitiesAsync(Guid projectId)
    {
        var repository = _unitOfWork.Repository<ProjectBuilding>();
        if (repository == null)
        {
            return [];
        }

        return (await repository.FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId))
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToList();
    }

    private async Task<List<ProjectFloor>> GetProjectFloorEntitiesAsync(Guid projectId)
    {
        var repository = _unitOfWork.Repository<ProjectFloor>();
        if (repository == null)
        {
            return [];
        }

        return (await repository.FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId))
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.LevelNumber ?? int.MaxValue)
            .ThenBy(x => x.Name)
            .ToList();
    }

    private async Task<List<ProjectUnitReleaseBatch>> GetProjectUnitReleaseBatchEntitiesAsync(Guid projectId)
    {
        var repository = _unitOfWork.Repository<ProjectUnitReleaseBatch>();
        if (repository == null)
        {
            return [];
        }

        return (await repository.FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId))
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToList();
    }

    private async Task<List<ProjectUnitHandoverBatch>> GetProjectUnitHandoverBatchEntitiesAsync(Guid projectId)
    {
        var repository = _unitOfWork.Repository<ProjectUnitHandoverBatch>();
        if (repository == null)
        {
            return [];
        }

        return (await repository.FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId))
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToList();
    }

    private async Task<List<ProjectUnitReleaseBatch>> GetProjectUnitReleaseBatchLookupAsync(IEnumerable<Guid> ids)
    {
        var distinctIds = ids.Distinct().ToList();
        if (distinctIds.Count == 0)
        {
            return [];
        }

        var repository = _unitOfWork.Repository<ProjectUnitReleaseBatch>();
        if (repository == null)
        {
            return [];
        }

        return (await repository.FindAsync(x => x.TenantId == _currentUserProvider.TenantId && distinctIds.Contains(x.Id))).ToList();
    }

    private async Task<List<ProjectUnitHandoverBatch>> GetProjectUnitHandoverBatchLookupAsync(IEnumerable<Guid> ids)
    {
        var distinctIds = ids.Distinct().ToList();
        if (distinctIds.Count == 0)
        {
            return [];
        }

        var repository = _unitOfWork.Repository<ProjectUnitHandoverBatch>();
        if (repository == null)
        {
            return [];
        }

        return (await repository.FindAsync(x => x.TenantId == _currentUserProvider.TenantId && distinctIds.Contains(x.Id))).ToList();
    }

    private async Task<ProjectBuilding> GetProjectBuildingEntityAsync(Guid buildingId)
        => await _unitOfWork.Repository<ProjectBuilding>().FirstOrDefaultAsync(x =>
               x.Id == buildingId
               && x.TenantId == _currentUserProvider.TenantId)
           ?? throw new InvalidOperationException($"Project building with ID {buildingId} not found");

    private async Task<ProjectFloor> GetProjectFloorEntityAsync(Guid floorId)
        => await _unitOfWork.Repository<ProjectFloor>().FirstOrDefaultAsync(x =>
               x.Id == floorId
               && x.TenantId == _currentUserProvider.TenantId)
           ?? throw new InvalidOperationException($"Project floor with ID {floorId} not found");

    private async Task<ProjectUnitReleaseBatch> GetProjectUnitReleaseBatchEntityAsync(Guid batchId)
        => await _unitOfWork.Repository<ProjectUnitReleaseBatch>().FirstOrDefaultAsync(x =>
               x.Id == batchId
               && x.TenantId == _currentUserProvider.TenantId)
           ?? throw new InvalidOperationException($"Project unit release batch with ID {batchId} not found");

    private async Task<ProjectUnitHandoverBatch> GetProjectUnitHandoverBatchEntityAsync(Guid batchId)
        => await _unitOfWork.Repository<ProjectUnitHandoverBatch>().FirstOrDefaultAsync(x =>
               x.Id == batchId
               && x.TenantId == _currentUserProvider.TenantId)
           ?? throw new InvalidOperationException($"Project unit handover batch with ID {batchId} not found");

    private async Task<ProjectBuildingDto> GetProjectBuildingDtoAsync(Guid projectId, Guid buildingId)
        => MapProjectBuildings((await GetProjectBuildingEntitiesAsync(projectId)).ToList()).Single(x => x.Id == buildingId);

    private async Task<ProjectFloorDto> GetProjectFloorDtoAsync(Guid projectId, Guid floorId)
        => (await MapProjectFloorsAsync((await GetProjectFloorEntitiesAsync(projectId)).ToList())).Single(x => x.Id == floorId);

    private async Task<ProjectUnitReleaseBatchDto> GetProjectUnitReleaseBatchDtoAsync(Guid projectId, Guid batchId)
        => (await MapProjectUnitReleaseBatchesAsync((await GetProjectUnitReleaseBatchEntitiesAsync(projectId)).ToList())).Single(x => x.Id == batchId);

    private async Task<ProjectUnitHandoverBatchDto> GetProjectUnitHandoverBatchDtoAsync(Guid projectId, Guid batchId)
        => (await MapProjectUnitHandoverBatchesAsync((await GetProjectUnitHandoverBatchEntitiesAsync(projectId)).ToList())).Single(x => x.Id == batchId);

    private static List<ProjectBuildingDto> MapProjectBuildings(IReadOnlyCollection<ProjectBuilding> buildings)
        => buildings
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Select(x => new ProjectBuildingDto
            {
                Id = x.Id,
                ProjectId = x.ProjectId,
                Code = x.Code,
                Name = x.Name,
                SortOrder = x.SortOrder,
                Notes = x.Notes,
            })
            .ToList();

    private async Task<List<ProjectFloorDto>> MapProjectFloorsAsync(IReadOnlyCollection<ProjectFloor> floors)
    {
        var buildingLookup = (await GetBusinessHierarchyBuildingLookupAsync(floors.Where(x => x.ProjectBuildingId.HasValue).Select(x => x.ProjectBuildingId!.Value)))
            .ToDictionary(x => x.Id);

        return floors
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.LevelNumber ?? int.MaxValue)
            .ThenBy(x => x.Name)
            .Select(x =>
            {
                buildingLookup.TryGetValue(x.ProjectBuildingId ?? Guid.Empty, out var building);
                return new ProjectFloorDto
                {
                    Id = x.Id,
                    ProjectId = x.ProjectId,
                    ProjectBuildingId = x.ProjectBuildingId,
                    ProjectBuildingCode = building?.Code,
                    ProjectBuildingName = building?.Name,
                    Code = x.Code,
                    Name = x.Name,
                    LevelNumber = x.LevelNumber,
                    SortOrder = x.SortOrder,
                    Notes = x.Notes,
                };
            })
            .ToList();
    }

    private async Task<List<ProjectUnitReleaseBatchDto>> MapProjectUnitReleaseBatchesAsync(IReadOnlyCollection<ProjectUnitReleaseBatch> batches)
    {
        var buildingLookup = (await GetBusinessHierarchyBuildingLookupAsync(batches.Where(x => x.ProjectBuildingId.HasValue).Select(x => x.ProjectBuildingId!.Value)))
            .ToDictionary(x => x.Id);
        var floorLookup = (await GetBusinessHierarchyFloorLookupAsync(batches.Where(x => x.ProjectFloorId.HasValue).Select(x => x.ProjectFloorId!.Value)))
            .ToDictionary(x => x.Id);

        return batches
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Select(x =>
            {
                buildingLookup.TryGetValue(x.ProjectBuildingId ?? Guid.Empty, out var building);
                floorLookup.TryGetValue(x.ProjectFloorId ?? Guid.Empty, out var floor);
                return new ProjectUnitReleaseBatchDto
                {
                    Id = x.Id,
                    ProjectId = x.ProjectId,
                    ProjectBuildingId = x.ProjectBuildingId,
                    ProjectBuildingCode = building?.Code,
                    ProjectBuildingName = building?.Name,
                    ProjectFloorId = x.ProjectFloorId,
                    ProjectFloorCode = floor?.Code,
                    ProjectFloorName = floor?.Name,
                    Code = x.Code,
                    Name = x.Name,
                    Status = x.Status,
                    PlannedReleaseDate = x.PlannedReleaseDate,
                    ActualReleaseDate = x.ActualReleaseDate,
                    SortOrder = x.SortOrder,
                    Notes = x.Notes,
                };
            })
            .ToList();
    }

    private async Task<List<ProjectUnitHandoverBatchDto>> MapProjectUnitHandoverBatchesAsync(IReadOnlyCollection<ProjectUnitHandoverBatch> batches)
    {
        var buildingLookup = (await GetBusinessHierarchyBuildingLookupAsync(batches.Where(x => x.ProjectBuildingId.HasValue).Select(x => x.ProjectBuildingId!.Value)))
            .ToDictionary(x => x.Id);
        var floorLookup = (await GetBusinessHierarchyFloorLookupAsync(batches.Where(x => x.ProjectFloorId.HasValue).Select(x => x.ProjectFloorId!.Value)))
            .ToDictionary(x => x.Id);

        return batches
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Select(x =>
            {
                buildingLookup.TryGetValue(x.ProjectBuildingId ?? Guid.Empty, out var building);
                floorLookup.TryGetValue(x.ProjectFloorId ?? Guid.Empty, out var floor);
                return new ProjectUnitHandoverBatchDto
                {
                    Id = x.Id,
                    ProjectId = x.ProjectId,
                    ProjectBuildingId = x.ProjectBuildingId,
                    ProjectBuildingCode = building?.Code,
                    ProjectBuildingName = building?.Name,
                    ProjectFloorId = x.ProjectFloorId,
                    ProjectFloorCode = floor?.Code,
                    ProjectFloorName = floor?.Name,
                    Code = x.Code,
                    Name = x.Name,
                    Status = x.Status,
                    PlannedHandoverDate = x.PlannedHandoverDate,
                    ActualHandoverDate = x.ActualHandoverDate,
                    SortOrder = x.SortOrder,
                    Notes = x.Notes,
                };
            })
            .ToList();
    }

    private async Task<List<ProjectBuilding>> GetBusinessHierarchyBuildingLookupAsync(IEnumerable<Guid> ids)
    {
        var distinctIds = ids.Distinct().ToList();
        if (distinctIds.Count == 0)
        {
            return [];
        }

        var repository = _unitOfWork.Repository<ProjectBuilding>();
        if (repository == null)
        {
            return [];
        }

        return (await repository.FindAsync(x => x.TenantId == _currentUserProvider.TenantId && distinctIds.Contains(x.Id))).ToList();
    }

    private async Task<List<ProjectFloor>> GetBusinessHierarchyFloorLookupAsync(IEnumerable<Guid> ids)
    {
        var distinctIds = ids.Distinct().ToList();
        if (distinctIds.Count == 0)
        {
            return [];
        }

        var repository = _unitOfWork.Repository<ProjectFloor>();
        if (repository == null)
        {
            return [];
        }

        return (await repository.FindAsync(x => x.TenantId == _currentUserProvider.TenantId && distinctIds.Contains(x.Id))).ToList();
    }

    private static string NormalizeProjectUnitReleaseBatchStatus(string? value)
        => value?.Trim() switch
        {
            null or "" => ProjectUnitReleaseBatchStatuses.Draft,
            _ => value.Trim()
        };

    private static string NormalizeProjectUnitHandoverBatchStatus(string? value)
        => value?.Trim() switch
        {
            null or "" => ProjectUnitHandoverBatchStatuses.Planned,
            _ => value.Trim()
        };
}
