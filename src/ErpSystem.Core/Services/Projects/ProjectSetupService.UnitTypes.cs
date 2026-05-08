using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Projects;

namespace ErpSystem.Core.Services.Projects;

public partial class ProjectSetupService
{
    public async Task<IEnumerable<ProjectUnitTypeTemplateDto>> GetProjectUnitTypeTemplatesAsync(bool includeInactive = false)
    {
        EnsureAdministrationAccess();

        var templates = (await _unitOfWork.Repository<ProjectUnitTypeTemplate>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId
                && (includeInactive || x.IsActive)))
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToList();

        return await MapProjectUnitTypeTemplatesAsync(templates);
    }

    public async Task<ProjectUnitTypeTemplateDto> CreateProjectUnitTypeTemplateAsync(CreateProjectUnitTypeTemplateDto dto)
    {
        EnsureAdministrationAccess();

        var repository = _unitOfWork.Repository<ProjectUnitTypeTemplate>();
        var normalizedCode = NormalizeRequiredCode(dto.Code, "Unit type code");
        var existing = (await repository.FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId
                && x.Code == normalizedCode))
            .FirstOrDefault();
        if (existing != null)
        {
            throw new InvalidOperationException($"A unit type template with code '{normalizedCode}' already exists.");
        }

        var entity = new ProjectUnitTypeTemplate
        {
            TenantId = _currentUserProvider.TenantId,
            Code = normalizedCode,
            Name = NormalizeRequiredName(dto.Name, "Unit type name"),
            Description = NormalizeOptionalValue(dto.Description),
            DefaultProjectUnitType = NormalizeProjectUnitTemplateCategory(dto.DefaultProjectUnitType),
            SortOrder = dto.SortOrder,
            IsActive = dto.IsActive,
            Currency = NormalizeCurrency(dto.Currency),
            CreatedBy = _currentUserProvider.Username,
            CreatedById = _currentUserProvider.UserId
        };

        await repository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        await ReplaceProjectUnitTypeTemplateAmenitiesAsync(entity.Id, dto.Amenities);
        return await GetProjectUnitTypeTemplateDtoAsync(entity.Id);
    }

    public async Task<ProjectUnitTypeTemplateDto> UpdateProjectUnitTypeTemplateAsync(Guid id, CreateProjectUnitTypeTemplateDto dto)
    {
        EnsureAdministrationAccess();

        var repository = _unitOfWork.Repository<ProjectUnitTypeTemplate>();
        var entity = await repository.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"Project unit type template with ID {id} not found.");

        var normalizedCode = NormalizeRequiredCode(dto.Code, "Unit type code");
        var duplicate = (await repository.FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId
                && x.Code == normalizedCode))
            .FirstOrDefault(x => x.Id != id);
        if (duplicate != null)
        {
            throw new InvalidOperationException($"A unit type template with code '{normalizedCode}' already exists.");
        }

        entity.Code = normalizedCode;
        entity.Name = NormalizeRequiredName(dto.Name, "Unit type name");
        entity.Description = NormalizeOptionalValue(dto.Description);
        entity.DefaultProjectUnitType = NormalizeProjectUnitTemplateCategory(dto.DefaultProjectUnitType);
        entity.SortOrder = dto.SortOrder;
        entity.IsActive = dto.IsActive;
        entity.Currency = NormalizeCurrency(dto.Currency);
        entity.UpdatedBy = _currentUserProvider.Username;
        entity.LastModifiedById = _currentUserProvider.UserId;

        await repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        await ReplaceProjectUnitTypeTemplateAmenitiesAsync(entity.Id, dto.Amenities);
        return await GetProjectUnitTypeTemplateDtoAsync(entity.Id);
    }

    public async Task DeleteProjectUnitTypeTemplateAsync(Guid id)
    {
        EnsureAdministrationAccess();

        var templateRepository = _unitOfWork.Repository<ProjectUnitTypeTemplate>();
        var amenityRepository = _unitOfWork.Repository<ProjectUnitTypeTemplateAmenity>();
        var template = await templateRepository.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"Project unit type template with ID {id} not found.");

        var amenities = (await amenityRepository.FindAsync(x =>
                x.ProjectUnitTypeTemplateId == id
                && x.TenantId == _currentUserProvider.TenantId))
            .ToList();
        if (amenities.Count > 0)
        {
            await amenityRepository.DeleteRangeAsync(amenities);
        }

        await templateRepository.DeleteAsync(template);
        await _unitOfWork.SaveChangesAsync();
    }

    private async Task<ProjectUnitTypeTemplateDto> GetProjectUnitTypeTemplateDtoAsync(Guid id)
    {
        var template = await _unitOfWork.Repository<ProjectUnitTypeTemplate>().GetByIdAsync(id)
            ?? throw new InvalidOperationException($"Project unit type template with ID {id} not found.");
        var mapped = await MapProjectUnitTypeTemplatesAsync([template]);
        return mapped.Single();
    }

    private async Task<List<ProjectUnitTypeTemplateDto>> MapProjectUnitTypeTemplatesAsync(IReadOnlyCollection<ProjectUnitTypeTemplate> templates)
    {
        if (templates.Count == 0)
        {
            return [];
        }

        var templateIds = templates.Select(x => x.Id).ToList();
        var amenities = (await _unitOfWork.Repository<ProjectUnitTypeTemplateAmenity>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId
                && templateIds.Contains(x.ProjectUnitTypeTemplateId)))
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.AmenityName)
            .ToList();
        var amenitiesByTemplateId = amenities
            .GroupBy(x => x.ProjectUnitTypeTemplateId)
            .ToDictionary(group => group.Key, group => group.ToList());

        return templates
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Select(template =>
            {
                var amenityDtos = amenitiesByTemplateId.TryGetValue(template.Id, out var amenityList)
                    ? amenityList.Select(MapToDto).ToList()
                    : [];

                return new ProjectUnitTypeTemplateDto
                {
                    Id = template.Id,
                    Code = template.Code,
                    Name = template.Name,
                    Description = template.Description,
                    DefaultProjectUnitType = template.DefaultProjectUnitType,
                    SortOrder = template.SortOrder,
                    IsActive = template.IsActive,
                    Currency = template.Currency,
                    TotalCost = amenityDtos.Sum(x => x.TotalCost),
                    Amenities = amenityDtos
                };
            })
            .ToList();
    }

    private async Task ReplaceProjectUnitTypeTemplateAmenitiesAsync(Guid templateId, IEnumerable<CreateProjectUnitTypeTemplateAmenityDto>? amenities)
    {
        var amenityRepository = _unitOfWork.Repository<ProjectUnitTypeTemplateAmenity>();
        var existing = (await amenityRepository.FindAsync(x =>
                x.ProjectUnitTypeTemplateId == templateId
                && x.TenantId == _currentUserProvider.TenantId))
            .ToList();
        if (existing.Count > 0)
        {
            await amenityRepository.DeleteRangeAsync(existing);
            await _unitOfWork.SaveChangesAsync();
        }

        var payload = (amenities ?? Enumerable.Empty<CreateProjectUnitTypeTemplateAmenityDto>()).ToList();
        if (payload.Count == 0)
        {
            return;
        }

        var inventoryIds = payload.Select(x => x.InventoryItemId).Distinct().ToList();
        var inventoryLookup = (await _unitOfWork.Repository<InventoryItem>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId
                && inventoryIds.Contains(x.Id)))
            .ToDictionary(x => x.Id);

        foreach (var amenity in payload.Select((value, index) => new { value, index }))
        {
            if (!inventoryLookup.TryGetValue(amenity.value.InventoryItemId, out var inventoryItem))
            {
                throw new InvalidOperationException("One or more selected amenity inventory items could not be found.");
            }

            var quantity = NormalizeAmenityQuantity(amenity.value.Quantity);
            var unitCost = NormalizeAmenityUnitCost(amenity.value.UnitCost, inventoryItem);
            await amenityRepository.AddAsync(new ProjectUnitTypeTemplateAmenity
            {
                TenantId = _currentUserProvider.TenantId,
                ProjectUnitTypeTemplateId = templateId,
                InventoryItemId = inventoryItem.Id,
                ItemCode = NormalizeOptionalValue(amenity.value.ItemCode) ?? inventoryItem.ItemCode,
                AmenityName = NormalizeRequiredName(amenity.value.AmenityName ?? inventoryItem.Name, "Amenity name"),
                Quantity = quantity,
                UnitCost = unitCost,
                SortOrder = amenity.value.SortOrder ?? ((amenity.index + 1) * 10),
                CreatedBy = _currentUserProvider.Username,
                CreatedById = _currentUserProvider.UserId
            });
        }

        await _unitOfWork.SaveChangesAsync();
    }

    private static ProjectUnitTypeTemplateAmenityDto MapToDto(ProjectUnitTypeTemplateAmenity entity) => new()
    {
        Id = entity.Id,
        InventoryItemId = entity.InventoryItemId,
        ItemCode = entity.ItemCode,
        AmenityName = entity.AmenityName,
        Quantity = entity.Quantity,
        UnitCost = entity.UnitCost,
        TotalCost = decimal.Round(entity.Quantity * entity.UnitCost, 2, MidpointRounding.AwayFromZero),
        SortOrder = entity.SortOrder
    };

    private static decimal NormalizeAmenityQuantity(decimal quantity)
    {
        if (quantity <= 0m)
        {
            throw new InvalidOperationException("Amenity quantity must be greater than zero.");
        }

        return decimal.Round(quantity, 2, MidpointRounding.AwayFromZero);
    }

    private static decimal NormalizeAmenityUnitCost(decimal unitCost, InventoryItem inventoryItem)
    {
        if (unitCost > 0m)
        {
            return decimal.Round(unitCost, 2, MidpointRounding.AwayFromZero);
        }

        var resolved = inventoryItem.StandardCost > 0m
            ? inventoryItem.StandardCost
            : inventoryItem.AverageCost > 0m
                ? inventoryItem.AverageCost
                : inventoryItem.LastPurchaseCost;

        return decimal.Round(Math.Max(0m, resolved), 2, MidpointRounding.AwayFromZero);
    }

    private static string NormalizeProjectUnitTemplateCategory(string? value)
        => value?.Trim() switch
        {
            ProjectUnitTypes.WholeBuilding => ProjectUnitTypes.WholeBuilding,
            ProjectUnitTypes.Apartment => ProjectUnitTypes.Apartment,
            ProjectUnitTypes.OfficeSuite => ProjectUnitTypes.OfficeSuite,
            ProjectUnitTypes.RetailShop => ProjectUnitTypes.RetailShop,
            ProjectUnitTypes.Warehouse => ProjectUnitTypes.Warehouse,
            _ => ProjectUnitTypes.Unit
        };

    private static string? NormalizeCurrency(string? value)
    {
        var normalized = NormalizeOptionalValue(value);
        return normalized?.ToUpperInvariant();
    }
}
