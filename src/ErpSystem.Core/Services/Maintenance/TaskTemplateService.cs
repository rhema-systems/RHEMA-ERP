using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Maintenance;

/// <summary>
/// Service for managing task templates across asset, asset type, and maintenance type levels
/// </summary>
public class TaskTemplateService : ITaskTemplateService
{
    private readonly IAssetTaskTemplateRepository _assetTaskTemplateRepository;
    private readonly IAssetTypeTaskTemplateRepository _assetTypeTaskTemplateRepository;
    private readonly IMaintenanceTaskTemplateRepository _maintenanceTaskTemplateRepository;
    private readonly IMaintenanceAssetRepository _assetRepository;
    private readonly IMaintenanceTypeRepository _maintenanceTypeRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<TaskTemplateService> _logger;
    private readonly IUnitOfWork _unitOfWork;

    public TaskTemplateService(
        IAssetTaskTemplateRepository assetTaskTemplateRepository,
        IAssetTypeTaskTemplateRepository assetTypeTaskTemplateRepository,
        IMaintenanceTaskTemplateRepository maintenanceTaskTemplateRepository,
        IMaintenanceAssetRepository assetRepository,
        IMaintenanceTypeRepository maintenanceTypeRepository,
        ICurrentUserService currentUserService,
        ILogger<TaskTemplateService> logger,
        IUnitOfWork unitOfWork)
    {
        _assetTaskTemplateRepository = assetTaskTemplateRepository;
        _assetTypeTaskTemplateRepository = assetTypeTaskTemplateRepository;
        _maintenanceTaskTemplateRepository = maintenanceTaskTemplateRepository;
        _assetRepository = assetRepository;
        _maintenanceTypeRepository = maintenanceTypeRepository;
        _currentUserService = currentUserService;
        _logger = logger;
        _unitOfWork = unitOfWork;
    }

    #region Get Task Templates for Work Order

    /// <summary>
    /// Gets task templates for work order generation by combining all applicable templates:
    /// 1. MaintenanceTaskTemplates (system-wide defaults for this maintenance type)
    /// 2. AssetTypeTaskTemplates (templates for this asset's type)
    /// 3. AssetTaskTemplates (asset-specific templates - highest priority)
    /// All templates are combined and ordered by their sequence numbers.
    /// </summary>
    public async Task<IEnumerable<WorkOrderTaskDto>> GetTaskTemplatesForWorkOrderAsync(Guid assetId, Guid maintenanceTypeId)
    {
        try
        {
            _logger.LogInformation("Getting task templates for asset {AssetId} and maintenance type {MaintenanceTypeId}",
                assetId, maintenanceTypeId);

            var allTasks = new List<WorkOrderTaskDto>();
            int sequenceOffset = 0;

            // 1. Get maintenance type templates (system defaults - lowest priority, loaded first)
            var maintenanceTypeTemplates = await _maintenanceTaskTemplateRepository
                .GetOrderedBySequenceAsync(maintenanceTypeId);

            if (maintenanceTypeTemplates?.Any() == true)
            {
                var mappedTemplates = maintenanceTypeTemplates.Select(t => MapMaintenanceTaskTemplateToDto(t)).ToList();
                _logger.LogInformation("Found {Count} maintenance type templates", mappedTemplates.Count);
                allTasks.AddRange(mappedTemplates);
                sequenceOffset = mappedTemplates.Max(t => t.Sequence) + 10; // Leave gap for organization
            }

            // 2. Get asset type templates (medium priority)
            // Need to get the asset's AssetType first
            var asset = await _assetRepository.GetByIdAsync(assetId);
            if (asset != null)
            {
                // Get AssetType based on the asset's category
                // This requires looking up AssetType by matching the category's AssetType string property
                // For now, we'll try to find AssetTypeTaskTemplates through a join
                try
                {
                    // Get all asset type templates for this maintenance type
                    var assetTypeTemplates = await _assetTypeTaskTemplateRepository.GetByMaintenanceTypeIdAsync(maintenanceTypeId);

                    if (assetTypeTemplates?.Any() == true)
                    {
                        var mappedTemplates = assetTypeTemplates
                            .Select(t =>
                            {
                                var dto = MapAssetTypeTaskTemplateToDto(t);
                                dto.Sequence += sequenceOffset; // Offset to come after maintenance type templates
                                return dto;
                            })
                            .ToList();

                        _logger.LogInformation("Found {Count} asset type templates", mappedTemplates.Count);
                        allTasks.AddRange(mappedTemplates);
                        sequenceOffset = mappedTemplates.Max(t => t.Sequence) + 10;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not load asset type templates for asset {AssetId}", assetId);
                }
            }

            // 3. Get asset-specific templates (highest priority, loaded last)
            // Asset templates supersede all others, so we get ALL active templates for the asset
            // regardless of maintenance type
            var assetTemplates = await _assetTaskTemplateRepository
                .GetActiveByAssetIdAsync(assetId);

            if (assetTemplates?.Any() == true)
            {
                var mappedTemplates = assetTemplates
                    .Select(t =>
                    {
                        var dto = MapAssetTaskTemplateToDto(t);
                        dto.Sequence += sequenceOffset; // Offset to come after asset type templates
                        return dto;
                    })
                    .ToList();

                _logger.LogInformation("Found {Count} asset-specific templates (any maintenance type)", mappedTemplates.Count);
                allTasks.AddRange(mappedTemplates);
            }

            // Return combined list ordered by final sequence
            var result = allTasks.OrderBy(t => t.Sequence).ToList();
            _logger.LogInformation("Total {Count} tasks loaded from all template sources", result.Count);

            if (!result.Any())
            {
                _logger.LogWarning("No task templates found for asset {AssetId} and maintenance type {MaintenanceTypeId}",
                    assetId, maintenanceTypeId);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting task templates for asset {AssetId}", assetId);
            throw;
        }
    }

    #endregion

    #region Asset Task Templates

    public async Task<IEnumerable<AssetTaskTemplateDto>> GetAssetTaskTemplatesAsync(Guid assetId)
    {
        var templates = await _assetTaskTemplateRepository.GetByAssetIdAsync(assetId);
        return templates.Select(MapToAssetTaskTemplateDto);
    }

    public async Task<AssetTaskTemplateDto> CreateAssetTaskTemplateAsync(CreateAssetTaskTemplateDto createDto)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        var template = new AssetTaskTemplate
        {
            AssetId = createDto.AssetId,
            MaintenanceTypeId = createDto.MaintenanceTypeId,
            TaskName = createDto.TaskName,
            Description = createDto.Description,
            Sequence = createDto.Sequence,
            EstimatedHours = createDto.EstimatedHours,
            IsRequired = createDto.IsRequired,
            AssignedTechnicianId = createDto.AssignedTechnicianId,
            Instructions = createDto.Instructions,
            SafetyRequirements = createDto.SafetyRequirements,
            RequiredTools = createDto.RequiredTools,
            RequiredParts = createDto.RequiredParts,
            IsActive = createDto.IsActive,
            TenantId = tenantId
        };

        await _assetTaskTemplateRepository.AddAsync(template);
        await _unitOfWork.SaveChangesAsync();

        return MapToAssetTaskTemplateDto(template);
    }

    public async Task<AssetTaskTemplateDto> UpdateAssetTaskTemplateAsync(Guid id, UpdateAssetTaskTemplateDto updateDto)
    {
        var template = await _assetTaskTemplateRepository.GetByIdAsync(id) ?? throw new KeyNotFoundException($"Asset task template {id} not found");
        template.TaskName = updateDto.TaskName;
        template.Description = updateDto.Description;
        template.Sequence = updateDto.Sequence;
        template.EstimatedHours = updateDto.EstimatedHours;
        template.IsRequired = updateDto.IsRequired;
        template.AssignedTechnicianId = updateDto.AssignedTechnicianId;
        template.Instructions = updateDto.Instructions;
        template.SafetyRequirements = updateDto.SafetyRequirements;
        template.RequiredTools = updateDto.RequiredTools;
        template.RequiredParts = updateDto.RequiredParts;
        template.IsActive = updateDto.IsActive;

        await _assetTaskTemplateRepository.UpdateAsync(template);
        await _unitOfWork.SaveChangesAsync();

        return MapToAssetTaskTemplateDto(template);
    }

    public async Task DeleteAssetTaskTemplateAsync(Guid id)
    {
        await _assetTaskTemplateRepository.DeleteAsync(id);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<IEnumerable<AssetTaskTemplateDto>> GetAllAssetTaskTemplatesAsync()
    {
        var templates = await _assetTaskTemplateRepository.GetAllAsync();
        return templates.Select(MapToAssetTaskTemplateDto);
    }

    #endregion

    #region Asset Type Task Templates

    public async Task<IEnumerable<AssetTypeTaskTemplateDto>> GetAssetTypeTaskTemplatesAsync(Guid assetTypeId)
    {
        var templates = await _assetTypeTaskTemplateRepository.GetByAssetTypeIdAsync(assetTypeId);
        return templates.Select(MapToAssetTypeTaskTemplateDto);
    }

    public async Task<AssetTypeTaskTemplateDto> CreateAssetTypeTaskTemplateAsync(CreateAssetTypeTaskTemplateDto createDto)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        var template = new AssetTypeTaskTemplate
        {
            AssetTypeId = createDto.AssetTypeId,
            MaintenanceTypeId = createDto.MaintenanceTypeId,
            TaskName = createDto.TaskName,
            Description = createDto.Description,
            Sequence = createDto.Sequence,
            EstimatedHours = createDto.EstimatedHours,
            IsRequired = createDto.IsRequired,
            AssignedTechnicianId = createDto.AssignedTechnicianId,
            Instructions = createDto.Instructions,
            SafetyRequirements = createDto.SafetyRequirements,
            RequiredTools = createDto.RequiredTools,
            RequiredParts = createDto.RequiredParts,
            IsActive = createDto.IsActive,
            TenantId = tenantId
        };

        await _assetTypeTaskTemplateRepository.AddAsync(template);
        await _unitOfWork.SaveChangesAsync();

        return MapToAssetTypeTaskTemplateDto(template);
    }

    public async Task<AssetTypeTaskTemplateDto> UpdateAssetTypeTaskTemplateAsync(Guid id, UpdateAssetTypeTaskTemplateDto updateDto)
    {
        var template = await _assetTypeTaskTemplateRepository.GetByIdAsync(id) ?? throw new KeyNotFoundException($"Asset type task template {id} not found");
        template.TaskName = updateDto.TaskName;
        template.Description = updateDto.Description;
        template.Sequence = updateDto.Sequence;
        template.EstimatedHours = updateDto.EstimatedHours;
        template.IsRequired = updateDto.IsRequired;
        template.AssignedTechnicianId = updateDto.AssignedTechnicianId;
        template.Instructions = updateDto.Instructions;
        template.SafetyRequirements = updateDto.SafetyRequirements;
        template.RequiredTools = updateDto.RequiredTools;
        template.RequiredParts = updateDto.RequiredParts;
        template.IsActive = updateDto.IsActive;

        await _assetTypeTaskTemplateRepository.UpdateAsync(template);
        await _unitOfWork.SaveChangesAsync();

        return MapToAssetTypeTaskTemplateDto(template);
    }

    public async Task DeleteAssetTypeTaskTemplateAsync(Guid id)
    {
        await _assetTypeTaskTemplateRepository.DeleteAsync(id);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<IEnumerable<AssetTypeTaskTemplateDto>> GetAllAssetTypeTaskTemplatesAsync()
    {
        var templates = await _assetTypeTaskTemplateRepository.GetAllAsync();
        return templates.Select(MapToAssetTypeTaskTemplateDto);
    }

    #endregion

    #region Maintenance Type Task Templates

    public async Task<IEnumerable<MaintenanceTaskTemplateDto>> GetMaintenanceTaskTemplatesAsync(Guid maintenanceTypeId)
    {
        var templates = await _maintenanceTaskTemplateRepository.GetByMaintenanceTypeIdAsync(maintenanceTypeId);
        return templates.Select(MapToMaintenanceTaskTemplateDto);
    }

    public async Task<IEnumerable<MaintenanceTaskTemplateDto>> GetAllMaintenanceTaskTemplatesAsync()
    {
        var templates = await _maintenanceTaskTemplateRepository.GetAllAsync();
        return templates.Select(MapToMaintenanceTaskTemplateDto);
    }

    public async Task<MaintenanceTaskTemplateDto> CreateMaintenanceTaskTemplateAsync(CreateMaintenanceTaskTemplateDto createDto)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        var template = new MaintenanceTaskTemplate
        {
            MaintenanceTypeId = createDto.MaintenanceTypeId,
            TaskName = createDto.TaskName,
            Description = createDto.Description,
            Sequence = createDto.Sequence,
            EstimatedHours = createDto.EstimatedHours,
            IsRequired = createDto.IsRequired,
            AssignedTechnicianId = createDto.AssignedTechnicianId,
            Instructions = createDto.Instructions,
            SafetyRequirements = createDto.SafetyRequirements,
            IsActive = createDto.IsActive,
            TenantId = tenantId
        };

        await _maintenanceTaskTemplateRepository.AddAsync(template);
        await _unitOfWork.SaveChangesAsync();

        return MapToMaintenanceTaskTemplateDto(template);
    }

    public async Task<MaintenanceTaskTemplateDto> UpdateMaintenanceTaskTemplateAsync(Guid id, UpdateMaintenanceTaskTemplateDto updateDto)
    {
        var template = await _maintenanceTaskTemplateRepository.GetByIdAsync(id) ?? throw new KeyNotFoundException($"Maintenance task template {id} not found");
        template.TaskName = updateDto.TaskName;
        template.Description = updateDto.Description;
        template.Sequence = updateDto.Sequence;
        template.EstimatedHours = updateDto.EstimatedHours;
        template.IsRequired = updateDto.IsRequired;
        template.AssignedTechnicianId = updateDto.AssignedTechnicianId;
        template.Instructions = updateDto.Instructions;
        template.SafetyRequirements = updateDto.SafetyRequirements;
        template.IsActive = updateDto.IsActive;

        await _maintenanceTaskTemplateRepository.UpdateAsync(template);
        await _unitOfWork.SaveChangesAsync();

        return MapToMaintenanceTaskTemplateDto(template);
    }

    public async Task DeleteMaintenanceTaskTemplateAsync(Guid id)
    {
        await _maintenanceTaskTemplateRepository.DeleteAsync(id);
        await _unitOfWork.SaveChangesAsync();
    }

    #endregion

    #region Helper Methods

    private static WorkOrderTaskDto MapAssetTaskTemplateToDto(AssetTaskTemplate template)
    {
        return new WorkOrderTaskDto
        {
            TaskName = template.TaskName,
            Description = template.Description,
            Sequence = template.Sequence,
            EstimatedHours = template.EstimatedHours,
            IsRequired = template.IsRequired,
            AssignedTechnicianId = template.AssignedTechnicianId,
            Instructions = template.Instructions,
            SafetyRequirements = template.SafetyRequirements,
            RequiredTools = template.RequiredTools,
            RequiredParts = template.RequiredParts,
            Source = "Asset"
        };
    }

    private static WorkOrderTaskDto MapAssetTypeTaskTemplateToDto(AssetTypeTaskTemplate template)
    {
        return new WorkOrderTaskDto
        {
            TaskName = template.TaskName,
            Description = template.Description,
            Sequence = template.Sequence,
            EstimatedHours = template.EstimatedHours,
            IsRequired = template.IsRequired,
            AssignedTechnicianId = template.AssignedTechnicianId,
            Instructions = template.Instructions,
            SafetyRequirements = template.SafetyRequirements,
            RequiredTools = template.RequiredTools,
            RequiredParts = template.RequiredParts,
            Source = "AssetType"
        };
    }

    private static WorkOrderTaskDto MapMaintenanceTaskTemplateToDto(MaintenanceTaskTemplate template)
    {
        return new WorkOrderTaskDto
        {
            TaskName = template.TaskName,
            Description = template.Description,
            Sequence = template.Sequence,
            EstimatedHours = template.EstimatedHours,
            IsRequired = template.IsRequired,
            AssignedTechnicianId = template.AssignedTechnicianId,
            Instructions = template.Instructions,
            SafetyRequirements = template.SafetyRequirements,
            RequiredTools = null,
            RequiredParts = null,
            Source = "MaintenanceType"
        };
    }

    private AssetTaskTemplateDto MapToAssetTaskTemplateDto(AssetTaskTemplate template)
    {
        return new AssetTaskTemplateDto
        {
            Id = template.Id,
            AssetId = template.AssetId,
            AssetName = template.Asset?.Name ?? string.Empty,
            MaintenanceTypeId = template.MaintenanceTypeId,
            MaintenanceTypeName = template.MaintenanceType?.Name ?? string.Empty,
            TaskName = template.TaskName,
            Description = template.Description,
            Sequence = template.Sequence,
            EstimatedHours = template.EstimatedHours,
            IsRequired = template.IsRequired,
            AssignedTechnicianId = template.AssignedTechnicianId,
            AssignedTechnicianName = template.AssignedTechnician?.FullName ?? string.Empty,
            Instructions = template.Instructions,
            SafetyRequirements = template.SafetyRequirements,
            RequiredTools = template.RequiredTools,
            RequiredParts = template.RequiredParts,
            IsActive = template.IsActive
        };
    }

    private AssetTypeTaskTemplateDto MapToAssetTypeTaskTemplateDto(AssetTypeTaskTemplate template)
    {
        return new AssetTypeTaskTemplateDto
        {
            Id = template.Id,
            AssetTypeId = template.AssetTypeId,
            AssetTypeName = template.AssetType?.Name ?? string.Empty,
            MaintenanceTypeId = template.MaintenanceTypeId,
            MaintenanceTypeName = template.MaintenanceType?.Name ?? string.Empty,
            TaskName = template.TaskName,
            Description = template.Description,
            Sequence = template.Sequence,
            EstimatedHours = template.EstimatedHours,
            IsRequired = template.IsRequired,
            AssignedTechnicianId = template.AssignedTechnicianId,
            AssignedTechnicianName = template.AssignedTechnician?.FullName ?? string.Empty,
            Instructions = template.Instructions,
            SafetyRequirements = template.SafetyRequirements,
            RequiredTools = template.RequiredTools,
            RequiredParts = template.RequiredParts,
            IsActive = template.IsActive
        };
    }

    private MaintenanceTaskTemplateDto MapToMaintenanceTaskTemplateDto(MaintenanceTaskTemplate template)
    {
        return new MaintenanceTaskTemplateDto
        {
            Id = template.Id,
            MaintenanceTypeId = template.MaintenanceTypeId,
            MaintenanceTypeName = template.MaintenanceType?.Name ?? string.Empty,
            TaskName = template.TaskName,
            Description = template.Description,
            Sequence = template.Sequence,
            EstimatedHours = template.EstimatedHours,
            IsRequired = template.IsRequired,
            AssignedTechnicianId = template.AssignedTechnicianId,
            AssignedTechnicianName = template.AssignedTechnician?.FullName ?? string.Empty,
            Instructions = template.Instructions,
            SafetyRequirements = template.SafetyRequirements,
            IsActive = template.IsActive
        };
    }

    #endregion
}
