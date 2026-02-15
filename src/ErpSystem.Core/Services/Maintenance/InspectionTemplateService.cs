using System.Text.Json;
using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Maintenance;

public class InspectionTemplateService : IInspectionTemplateService
{
    private readonly ILogger<InspectionTemplateService> _logger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;

    public InspectionTemplateService(IUnitOfWork unitOfWork, ICurrentUserProvider currentUserProvider, ILogger<InspectionTemplateService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    #region CRUD Operations

    public async Task<InspectionTemplateDto> CreateTemplateAsync(CreateInspectionTemplateDto createDto)
    {
        try
        {
            _logger.LogInformation("Creating inspection template: {TemplateName}", createDto.Name);

            createDto ??= new CreateInspectionTemplateDto();
            if (string.IsNullOrWhiteSpace(createDto.Name)) throw new ArgumentException("Name is required.");
            if (string.IsNullOrWhiteSpace(createDto.Code)) throw new ArgumentException("Code is required.");

            var tenantId = _currentUserProvider.TenantId;
            var userId = _currentUserProvider.UserId;
            var now = DateTime.UtcNow;

            var repo = _unitOfWork.Repository<InspectionTemplate>();
            var exists = await repo.FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Code == createDto.Code.Trim() && !t.IsDeleted);
            if (exists != null) throw new InvalidOperationException($"An inspection template with code '{createDto.Code}' already exists.");

            var entity = new InspectionTemplate
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = createDto.Name.Trim(),
                Code = createDto.Code.Trim(),
                Description = string.IsNullOrWhiteSpace(createDto.Description) ? null : createDto.Description.Trim(),
                Category = createDto.Category.Trim(),
                Frequency = createDto.Frequency.Trim(),
                EstimatedDuration = createDto.EstimatedDuration,
                RequiresSignature = createDto.RequiresSignature,
                AllowPhotos = createDto.AllowPhotos,
                Version = string.IsNullOrWhiteSpace(createDto.Version) ? "1.0" : createDto.Version.Trim(),
                Priority = string.IsNullOrWhiteSpace(createDto.Priority) ? "Medium" : createDto.Priority.Trim(),
                AssetTypes = JsonSerializer.Serialize(createDto.AssetTypes ?? new List<string>()),
                InspectorRoles = JsonSerializer.Serialize(createDto.InspectorRoles ?? new List<string>()),
                ChecklistItems = JsonSerializer.Serialize((createDto.ChecklistItems ?? new List<CreateInspectionChecklistItemDto>())
                    .Select((i, idx) => new
                    {
                        id = Guid.NewGuid(),
                        item = i.Item,
                        type = i.Type,
                        required = i.Required,
                        order = i.Order == 0 ? (idx + 1) : i.Order
                    })),
                InspectionType = string.IsNullOrWhiteSpace(createDto.Category) ? "General" : createDto.Category.Trim(),
                IsActive = createDto.IsActive,
                CreatedAt = now,
                CreatedById = userId
            };

            await repo.AddAsync(entity);
            await _unitOfWork.SaveChangesAsync();

            return Map(entity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating inspection template: {TemplateName}", createDto.Name);
            throw;
        }
    }

    public async Task<InspectionTemplateDto> UpdateTemplateAsync(Guid id, UpdateInspectionTemplateDto updateDto)
    {
        try
        {
            _logger.LogInformation("Updating inspection template: {TemplateId}", id);

            if (id == Guid.Empty) throw new ArgumentException("Id is required.");
            updateDto ??= new UpdateInspectionTemplateDto();

            var tenantId = _currentUserProvider.TenantId;
            var userId = _currentUserProvider.UserId;
            var now = DateTime.UtcNow;

            var repo = _unitOfWork.Repository<InspectionTemplate>();
            var entity = await repo.FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Id == id && !t.IsDeleted)
                ?? throw new ArgumentException("Template not found.");

            if (!string.Equals(entity.Code, updateDto.Code.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                var exists = await repo.FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Code == updateDto.Code.Trim() && !t.IsDeleted);
                if (exists != null) throw new InvalidOperationException($"An inspection template with code '{updateDto.Code}' already exists.");
                entity.Code = updateDto.Code.Trim();
            }

            entity.Name = updateDto.Name.Trim();
            entity.Description = string.IsNullOrWhiteSpace(updateDto.Description) ? null : updateDto.Description.Trim();
            entity.Category = updateDto.Category.Trim();
            entity.Frequency = updateDto.Frequency.Trim();
            entity.EstimatedDuration = updateDto.EstimatedDuration;
            entity.RequiresSignature = updateDto.RequiresSignature;
            entity.AllowPhotos = updateDto.AllowPhotos;
            entity.Version = string.IsNullOrWhiteSpace(updateDto.Version) ? entity.Version : updateDto.Version.Trim();
            entity.Priority = string.IsNullOrWhiteSpace(updateDto.Priority) ? entity.Priority : updateDto.Priority.Trim();
            entity.AssetTypes = JsonSerializer.Serialize(updateDto.AssetTypes ?? new List<string>());
            entity.InspectorRoles = JsonSerializer.Serialize(updateDto.InspectorRoles ?? new List<string>());
            entity.ChecklistItems = JsonSerializer.Serialize((updateDto.ChecklistItems ?? new List<CreateInspectionChecklistItemDto>())
                .Select((i, idx) => new
                {
                    id = Guid.NewGuid(),
                    item = i.Item,
                    type = i.Type,
                    required = i.Required,
                    order = i.Order == 0 ? (idx + 1) : i.Order
                }));
            entity.InspectionType = string.IsNullOrWhiteSpace(updateDto.Category) ? entity.InspectionType : updateDto.Category.Trim();
            entity.IsActive = updateDto.IsActive;
            entity.UpdatedAt = now;
            entity.LastModifiedById = userId;

            await repo.UpdateAsync(entity);
            await _unitOfWork.SaveChangesAsync();

            return Map(entity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating inspection template: {TemplateId}", id);
            throw;
        }
    }

    public async Task DeleteTemplateAsync(Guid id)
    {
        try
        {
            _logger.LogInformation("Deleting inspection template: {TemplateId}", id);

            if (id == Guid.Empty) return;

            var tenantId = _currentUserProvider.TenantId;
            var userId = _currentUserProvider.UserId;
            var now = DateTime.UtcNow;

            var repo = _unitOfWork.Repository<InspectionTemplate>();
            var entity = await repo.FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Id == id && !t.IsDeleted);
            if (entity == null) return;

            entity.IsDeleted = true;
            entity.DeletedAt = now;
            entity.DeletedBy = userId.ToString();
            entity.UpdatedAt = now;
            entity.LastModifiedById = userId;

            await repo.UpdateAsync(entity);
            await _unitOfWork.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting inspection template: {TemplateId}", id);
            throw;
        }
    }

    public async Task<InspectionTemplateDto?> GetTemplateByIdAsync(Guid id)
    {
        if (id == Guid.Empty) return null;

        var tenantId = _currentUserProvider.TenantId;
        var repo = _unitOfWork.Repository<InspectionTemplate>();

        var entity = await repo.FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Id == id && !t.IsDeleted);
        return entity == null ? null : Map(entity);
    }

    public async Task<IEnumerable<InspectionTemplateDto>> GetAllTemplatesAsync()
    {
        var tenantId = _currentUserProvider.TenantId;
        var repo = _unitOfWork.Repository<InspectionTemplate>();

        var entities = await repo.GetQueryable(t => t.TenantId == tenantId && !t.IsDeleted)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        return entities.Select(Map).ToList();
    }

    public async Task<IEnumerable<InspectionTemplateDto>> GetTemplatesByCategoryAsync(string category)
    {
        if (string.IsNullOrWhiteSpace(category)) return await GetAllTemplatesAsync();

        var tenantId = _currentUserProvider.TenantId;
        var repo = _unitOfWork.Repository<InspectionTemplate>();

        var entities = await repo.GetQueryable(t => t.TenantId == tenantId && !t.IsDeleted && t.Category == category)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        return entities.Select(Map).ToList();
    }

    public async Task<IEnumerable<InspectionTemplateDto>> GetActiveTemplatesAsync()
    {
        var tenantId = _currentUserProvider.TenantId;
        var repo = _unitOfWork.Repository<InspectionTemplate>();

        var entities = await repo.GetQueryable(t => t.TenantId == tenantId && !t.IsDeleted && t.IsActive)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        return entities.Select(Map).ToList();
    }

    #endregion

    private static InspectionTemplateDto Map(InspectionTemplate t)
    {
        var assetTypes = SafeJson<List<string>>(t.AssetTypes) ?? new List<string>();
        var inspectorRoles = SafeJson<List<string>>(t.InspectorRoles) ?? new List<string>();

        var checklist = SafeJson<List<JsonElement>>(t.ChecklistItems) ?? new List<JsonElement>();
        var checklistItems = new List<InspectionChecklistItemDto>();

        foreach (var item in checklist)
        {
            try
            {
                var id = item.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String && Guid.TryParse(idEl.GetString(), out var gid)
                    ? gid
                    : Guid.NewGuid();
                var text = item.TryGetProperty("item", out var itemEl) ? itemEl.GetString() ?? string.Empty : string.Empty;
                var type = item.TryGetProperty("type", out var typeEl) ? typeEl.GetString() ?? "checklist" : "checklist";
                var required = item.TryGetProperty("required", out var reqEl) && reqEl.ValueKind == JsonValueKind.True;
                var order = item.TryGetProperty("order", out var orderEl) && orderEl.TryGetInt32(out var ord) ? ord : 0;

                checklistItems.Add(new InspectionChecklistItemDto
                {
                    Id = id,
                    Item = text,
                    Type = type,
                    Required = required,
                    Order = order
                });
            }
            catch
            {
                // ignore bad item
            }
        }

        checklistItems = checklistItems.OrderBy(i => i.Order).ToList();

        return new InspectionTemplateDto
        {
            Id = t.Id,
            Name = t.Name,
            Code = t.Code,
            Description = t.Description ?? string.Empty,
            Category = t.Category ?? string.Empty,
            Frequency = t.Frequency,
            EstimatedDuration = t.EstimatedDuration,
            IsActive = t.IsActive,
            RequiresSignature = t.RequiresSignature,
            AllowPhotos = t.AllowPhotos,
            Version = t.Version,
            Priority = t.Priority,
            AssetTypes = assetTypes,
            InspectorRoles = inspectorRoles,
            ChecklistItems = checklistItems,
            CreatedAt = t.CreatedAt,
            UpdatedAt = t.UpdatedAt
        };
    }

    private static T? SafeJson<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return default;
        try
        {
            return JsonSerializer.Deserialize<T>(json);
        }
        catch
        {
            return default;
        }
    }
}
