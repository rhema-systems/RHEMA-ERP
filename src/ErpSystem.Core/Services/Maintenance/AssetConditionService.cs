using System.Text.Json;
using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Maintenance;
using ErpSystem.Core.Services;

namespace ErpSystem.Core.Services.Maintenance;

/// <summary>
/// Service for managing asset condition inspections (pre-inspection at admission / post-inspection at discharge)
/// </summary>
public class AssetConditionService : IAssetConditionService
{
    private readonly IAssetConditionRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IWorkOrderTaskRepository _taskRepository;
    private readonly IUserService _userService;

    public AssetConditionService(
        IAssetConditionRepository repository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IWorkOrderTaskRepository taskRepository,
        IUserService userService)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _taskRepository = taskRepository;
        _userService = userService;
    }

    private Guid TenantId => _currentUserService.TenantId ?? throw new UnauthorizedAccessException("Tenant not found");
    private Guid UserId => Guid.TryParse(_currentUserService.UserId, out var id) ? id : throw new UnauthorizedAccessException("User not found");

    #region Template Operations

    public async Task<AssetConditionChecklistTemplateDto> GetTemplateByIdAsync(Guid id)
    {
        var template = await _repository.GetTemplateByIdAsync(id, TenantId)
            ?? throw new KeyNotFoundException($"Template with ID {id} not found");
        return MapTemplateToDto(template);
    }

    public async Task<AssetConditionChecklistTemplateDto> GetTemplateWithItemsAsync(Guid id)
    {
        var template = await _repository.GetTemplateWithItemsAsync(id, TenantId)
            ?? throw new KeyNotFoundException($"Template with ID {id} not found");
        return MapTemplateToDto(template);
    }

    public async Task<IEnumerable<AssetConditionChecklistTemplateDto>> GetAllTemplatesAsync(bool includeInactive = false)
    {
        var templates = await _repository.GetAllTemplatesAsync(TenantId, includeInactive);
        return templates.Select(MapTemplateToDto);
    }

    public async Task<IEnumerable<AssetConditionChecklistTemplateDto>> GetTemplatesByAssetCategoryAsync(Guid assetCategoryId)
    {
        var templates = await _repository.GetTemplatesByAssetCategoryAsync(assetCategoryId, TenantId);
        return templates.Select(MapTemplateToDto);
    }

    public async Task<AssetConditionChecklistTemplateDto?> GetDefaultTemplateForAssetCategoryAsync(Guid assetCategoryId)
    {
        var template = await _repository.GetDefaultTemplateForAssetCategoryAsync(assetCategoryId, TenantId);
        return template != null ? MapTemplateToDto(template) : null;
    }

    public async Task<AssetConditionChecklistTemplateDto> CreateTemplateAsync(CreateAssetConditionTemplateDto dto)
    {
        var template = new PreInspectionChecklistTemplate
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            Name = dto.Name,
            Description = dto.Description,
            Category = dto.Category,
            AssetCategoryId = dto.AssetCategoryId,
            IsActive = dto.IsActive,
            IsDefault = dto.IsDefault,
            SortOrder = dto.SortOrder,
            Version = 1,
            CreatedAt = DateTime.UtcNow,
            CreatedById = UserId
        };

        // If this is default, unset other defaults for this asset category
        if (dto.IsDefault)
        {
            await UnsetDefaultForAssetCategoryAsync(dto.AssetCategoryId, null);
        }

        await _repository.AddTemplateAsync(template);

        // Add checklist items
        foreach (var itemDto in dto.ChecklistItems)
        {
            var item = CreateChecklistItem(template.Id, itemDto);
            await _repository.AddTemplateItemAsync(item);
        }

        await _unitOfWork.SaveChangesAsync();

        return await GetTemplateWithItemsAsync(template.Id);
    }

    public async Task<AssetConditionChecklistTemplateDto> UpdateTemplateAsync(Guid id, UpdateAssetConditionTemplateDto dto)
    {
        var template = await _repository.GetTemplateWithItemsAsync(id, TenantId)
            ?? throw new KeyNotFoundException($"Template with ID {id} not found");

        template.Name = dto.Name;
        template.Description = dto.Description;
        template.Category = dto.Category;
        template.AssetCategoryId = dto.AssetCategoryId;
        template.IsActive = dto.IsActive;
        template.IsDefault = dto.IsDefault;
        template.SortOrder = dto.SortOrder;
        template.Version++;
        template.VersionNotes = dto.VersionNotes;
        template.UpdatedAt = DateTime.UtcNow;
        template.LastModifiedById = UserId;

        // If this is default, unset other defaults for this asset category
        if (dto.IsDefault)
        {
            await UnsetDefaultForAssetCategoryAsync(dto.AssetCategoryId, id);
        }

        await _repository.UpdateTemplateAsync(template);

        // Update checklist items
        var existingItemIds = template.ChecklistItems.Select(i => i.Id).ToList();
        var updatedItemIds = dto.ChecklistItems.Where(i => i.Id.HasValue).Select(i => i.Id!.Value).ToList();

        // Delete removed items
        foreach (var itemId in existingItemIds.Except(updatedItemIds))
        {
            await _repository.DeleteTemplateItemAsync(itemId, TenantId);
        }

        // Update existing and add new items
        foreach (var itemDto in dto.ChecklistItems)
        {
            if (itemDto.Id.HasValue)
            {
                var existingItem = template.ChecklistItems.FirstOrDefault(i => i.Id == itemDto.Id.Value);
                if (existingItem != null)
                {
                    UpdateChecklistItem(existingItem, itemDto);
                    await _repository.UpdateTemplateItemAsync(existingItem);
                }
            }
            else
            {
                var newItem = CreateChecklistItem(template.Id, itemDto);
                await _repository.AddTemplateItemAsync(newItem);
            }
        }

        await _unitOfWork.SaveChangesAsync();
        return await GetTemplateWithItemsAsync(id);
    }

    public async Task DeleteTemplateAsync(Guid id)
    {
        await _repository.DeleteTemplateAsync(id, TenantId);
        await _unitOfWork.SaveChangesAsync();
    }

    #endregion

    #region Condition Record Operations

    public async Task<AssetConditionRecordDto> GetRecordByIdAsync(Guid id)
    {
        var record = await _repository.GetRecordByIdAsync(id, TenantId)
            ?? throw new KeyNotFoundException($"Asset condition record with ID {id} not found");
        return await MapRecordToDtoAsync(record);
    }

    public async Task<AssetConditionRecordDto> GetRecordWithDetailsAsync(Guid id)
    {
        var record = await _repository.GetRecordWithDetailsAsync(id, TenantId)
            ?? throw new KeyNotFoundException($"Asset condition record with ID {id} not found");
        return await MapRecordToDtoAsync(record);
    }

    public async Task<IEnumerable<AssetConditionRecordSummaryDto>> GetAllRecordsAsync(int page = 1, int pageSize = 20)
    {
        var records = await _repository.GetAllRecordsAsync(TenantId, page, pageSize);
        var results = new List<AssetConditionRecordSummaryDto>();
        foreach (var record in records)
        {
            results.Add(await MapRecordToSummaryDtoAsync(record));
        }
        return results;
    }

    public async Task<IEnumerable<AssetConditionRecordSummaryDto>> GetRecordsByAssetAsync(Guid assetId)
    {
        var records = await _repository.GetRecordsByAssetAsync(assetId, TenantId);
        var results = new List<AssetConditionRecordSummaryDto>();
        foreach (var record in records)
        {
            results.Add(await MapRecordToSummaryDtoAsync(record));
        }
        return results;
    }

    public async Task<AssetConditionRecordDto?> GetAdmissionRecordForAdmissionAsync(Guid admissionId)
    {
        var record = await _repository.GetAdmissionRecordForAdmissionAsync(admissionId, TenantId);
        return record != null ? await MapRecordToDtoAsync(record) : null;
    }

    public async Task<AssetConditionRecordDto?> GetDischargeRecordForAdmissionAsync(Guid admissionId)
    {
        var record = await _repository.GetDischargeRecordForAdmissionAsync(admissionId, TenantId);
        return record != null ? await MapRecordToDtoAsync(record) : null;
    }

    public async Task<AssetConditionRecordDto?> GetAdmissionRecordForJobCardAsync(Guid jobCardId)
    {
        var record = await _repository.GetAdmissionRecordForJobCardAsync(jobCardId, TenantId);
        return record != null ? await MapRecordToDtoAsync(record) : null;
    }

    public async Task<AssetConditionRecordDto?> GetDischargeRecordForJobCardAsync(Guid jobCardId)
    {
        var record = await _repository.GetDischargeRecordForJobCardAsync(jobCardId, TenantId);
        return record != null ? await MapRecordToDtoAsync(record) : null;
    }

    public async Task<AssetConditionRecordDto> StartConditionInspectionAsync(CreateAssetConditionRecordDto dto)
    {
        var template = await _repository.GetTemplateWithItemsAsync(dto.TemplateId, TenantId)
            ?? throw new KeyNotFoundException($"Template with ID {dto.TemplateId} not found");

        var prefix = dto.InspectionType == "Admission" ? "ADM" : "DIS";
        // Use provided InspectorId if available, otherwise default to current user
        var inspectorId = dto.InspectorId ?? UserId;
        var record = new AssetConditionRecord
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            InspectionNumber = $"{prefix}-{DateTime.UtcNow:yyyyMMddHHmmss}",
            AssetId = dto.AssetId,
            TemplateId = dto.TemplateId,
            InspectorId = inspectorId,
            InspectionDate = DateTime.UtcNow,
            InspectionType = dto.InspectionType,
            Status = "InProgress",
            GeneralNotes = dto.GeneralNotes,
            AdmissionId = dto.AdmissionId,
            CreatedAt = DateTime.UtcNow,
            CreatedById = UserId
        };

        await _repository.AddRecordAsync(record);

        // Create item results for each checklist item
        foreach (var item in template.ChecklistItems)
        {
            var itemResult = new AssetConditionItemResult
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                ConditionRecordId = record.Id,
                ChecklistItemId = item.Id,
                CreatedAt = DateTime.UtcNow,
                CreatedById = UserId
            };
            await _repository.AddItemResultAsync(itemResult);
        }

        await _unitOfWork.SaveChangesAsync();
        return await GetRecordWithDetailsAsync(record.Id);
    }

    public async Task<AssetConditionItemResultDto> SubmitItemResultAsync(Guid recordId, SubmitAssetConditionItemDto dto)
    {
        var record = await _repository.GetRecordByIdAsync(recordId, TenantId)
            ?? throw new KeyNotFoundException($"Asset condition record with ID {recordId} not found");

        if (record.Status == "Completed")
            throw new InvalidOperationException("Cannot update items on a completed condition record");

        var itemResult = await _repository.GetItemResultByRecordAndItemAsync(recordId, dto.ChecklistItemId, TenantId);

        var isNewItemResult = itemResult == null;
        if (isNewItemResult)
        {
            var checklistItem = await _repository.GetTemplateItemByIdAsync(dto.ChecklistItemId, TenantId)
                ?? throw new KeyNotFoundException($"Checklist item {dto.ChecklistItemId} not found");

            if (checklistItem.TemplateId != record.TemplateId)
            {
                throw new KeyNotFoundException($"Checklist item {dto.ChecklistItemId} does not belong to inspection template {record.TemplateId}");
            }

            itemResult = new AssetConditionItemResult
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                ConditionRecordId = record.Id,
                ChecklistItemId = checklistItem.Id,
                CreatedAt = DateTime.UtcNow,
                CreatedById = UserId
            };
        }

        itemResult.IsPresent = dto.IsPresent;
        itemResult.TextValue = dto.TextValue;
        itemResult.NumericValue = dto.NumericValue;
        itemResult.SelectedOption = dto.SelectedOption;
        itemResult.Comment = dto.Comment;
        itemResult.RepairReplacementAction = dto.RepairReplacementAction;
        // Set photo paths - replace with the provided list (frontend is responsible for managing the full list)
        if (dto.PhotoPaths != null)
        {
            itemResult.PhotoPaths = dto.PhotoPaths.Count > 0
                ? JsonSerializer.Serialize(dto.PhotoPaths)
                : null;
        }
        itemResult.InspectedAt = DateTime.UtcNow;
        itemResult.UpdatedAt = DateTime.UtcNow;
        itemResult.LastModifiedById = UserId;

        if (isNewItemResult)
        {
            await _repository.AddItemResultAsync(itemResult);
        }
        else
        {
            await _repository.UpdateItemResultAsync(itemResult);
        }

        await _unitOfWork.SaveChangesAsync();

        return MapItemResultToDto(itemResult);
    }

    public async Task<AssetConditionRecordDto> CompleteConditionInspectionAsync(Guid recordId, CompleteAssetConditionRecordDto dto)
    {
        var record = await _repository.GetRecordWithDetailsAsync(recordId, TenantId)
            ?? throw new KeyNotFoundException($"Asset condition record with ID {recordId} not found");

        if (record.Status == "Completed")
            throw new InvalidOperationException("Condition inspection is already completed");

        record.Status = "Completed";
        record.GeneralNotes = dto.GeneralNotes ?? record.GeneralNotes;
        record.UpdatedAt = DateTime.UtcNow;
        record.LastModifiedById = UserId;

        await _repository.UpdateRecordAsync(record);

        // For admission inspections, create work order tasks for items with repair/replacement actions
        if (record.InspectionType == "Admission" && record.AdmissionId.HasValue)
        {
            await CreateRepairReplacementTasksAsync(record);
        }

        await _unitOfWork.SaveChangesAsync();

        return await GetRecordWithDetailsAsync(recordId);
    }

    /// <summary>
    /// Creates work order tasks for checklist items that have repair/replacement actions selected
    /// </summary>
    private async Task CreateRepairReplacementTasksAsync(AssetConditionRecord record)
    {
        if (record.Admission?.WorkOrderId == null)
            return;

        var workOrderId = record.Admission.WorkOrderId.Value;

        // Get existing tasks to determine the next sequence number
        var existingTasks = await _taskRepository.GetByWorkOrderIdAsync(workOrderId);
        var nextSequence = existingTasks.Any() ? existingTasks.Max(t => t.Sequence) + 1 : 1;

        foreach (var itemResult in record.ItemResults.Where(ir => !ir.IsDeleted))
        {
            // Skip items without repair/replacement action or with "None" action
            if (string.IsNullOrEmpty(itemResult.RepairReplacementAction) ||
                itemResult.RepairReplacementAction == "None" ||
                itemResult.TaskCreated)
                continue;

            var checklistItem = itemResult.ChecklistItem;
            if (checklistItem == null || !checklistItem.AllowRepairReplacement)
                continue;

            // Determine estimated hours based on action
            var estimatedHours = itemResult.RepairReplacementAction == "Repair"
                ? checklistItem.EstimatedRepairHours
                : checklistItem.EstimatedReplacementHours;

            // Create the work order task
            var task = new WorkOrderTask
            {
                Id = Guid.NewGuid(),
                WorkOrderId = workOrderId,
                TaskName = $"{itemResult.RepairReplacementAction} - {checklistItem.ItemName}",
                Description = $"[From Admission Checklist] {checklistItem.Description ?? checklistItem.ItemName}. " +
                              $"Inspector comment: {itemResult.Comment ?? "No comment"}",
                Sequence = nextSequence++,
                EstimatedHours = estimatedHours,
                IsRequired = true,
                TenantId = TenantId,
                CreatedAt = DateTime.UtcNow,
                CreatedById = UserId,
                Status = "Pending"
            };

            await _taskRepository.AddAsync(task);

            // Mark the item result as having a task created
            itemResult.TaskCreated = true;
            itemResult.CreatedTaskId = task.Id;
            await _repository.UpdateItemResultAsync(itemResult);
        }
    }

    public async Task CancelConditionInspectionAsync(Guid recordId)
    {
        var record = await _repository.GetRecordByIdAsync(recordId, TenantId)
            ?? throw new KeyNotFoundException($"Asset condition record with ID {recordId} not found");

        if (record.Status == "Completed")
            throw new InvalidOperationException("Cannot cancel a completed condition inspection");

        record.Status = "Cancelled";
        record.UpdatedAt = DateTime.UtcNow;
        record.LastModifiedById = UserId;

        await _repository.UpdateRecordAsync(record);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task LinkToAdmissionAsync(Guid recordId, Guid admissionId)
    {
        var record = await _repository.GetRecordByIdAsync(recordId, TenantId)
            ?? throw new KeyNotFoundException($"Asset condition record with ID {recordId} not found");

        record.AdmissionId = admissionId;
        record.UpdatedAt = DateTime.UtcNow;
        record.LastModifiedById = UserId;

        await _repository.UpdateRecordAsync(record);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task LinkToDischargeAsync(Guid recordId, Guid dischargeId)
    {
        var record = await _repository.GetRecordByIdAsync(recordId, TenantId)
            ?? throw new KeyNotFoundException($"Asset condition record with ID {recordId} not found");

        record.DischargeId = dischargeId;
        record.UpdatedAt = DateTime.UtcNow;
        record.LastModifiedById = UserId;

        await _repository.UpdateRecordAsync(record);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<AssetConditionRecordDto> UploadItemPhotoAsync(Guid recordId, Guid itemResultId, string photoPath)
    {
        var itemResult = await _repository.GetItemResultByIdAsync(itemResultId, TenantId)
            ?? throw new KeyNotFoundException($"Item result with ID {itemResultId} not found");

        var photoPaths = string.IsNullOrEmpty(itemResult.PhotoPaths)
            ? new List<string>()
            : JsonSerializer.Deserialize<List<string>>(itemResult.PhotoPaths) ?? new List<string>();

        photoPaths.Add(photoPath);
        itemResult.PhotoPaths = JsonSerializer.Serialize(photoPaths);
        itemResult.UpdatedAt = DateTime.UtcNow;
        itemResult.LastModifiedById = UserId;

        await _repository.UpdateItemResultAsync(itemResult);
        await _unitOfWork.SaveChangesAsync();

        return await GetRecordWithDetailsAsync(recordId);
    }

    /// <summary>
    /// Creates work order tasks from completed admission checklist items that have repair/replacement actions.
    /// This is called after a work order is generated from a job card.
    /// </summary>
    public async Task<int> CreateTasksFromAdmissionChecklistAsync(Guid jobCardId, Guid workOrderId)
    {
        // Get the admission condition record for this job card
        var record = await _repository.GetAdmissionRecordForJobCardAsync(jobCardId, TenantId);

        if (record == null)
        {
            // No admission checklist found for this job card - that's OK, not all job cards have them
            return 0;
        }

        if (record.Status != "Completed")
        {
            // Checklist not completed yet - can't create tasks from incomplete checklist
            return 0;
        }

        // Load item results with checklist items
        var recordWithDetails = await _repository.GetRecordWithDetailsAsync(record.Id, TenantId);
        if (recordWithDetails == null)
        {
            return 0;
        }

        // Get existing tasks to determine the next sequence number
        var existingTasks = await _taskRepository.GetByWorkOrderIdAsync(workOrderId);
        var nextSequence = existingTasks.Any() ? existingTasks.Max(t => t.Sequence) + 1 : 1;

        var tasksCreated = 0;

        foreach (var itemResult in recordWithDetails.ItemResults.Where(ir => !ir.IsDeleted))
        {
            // Skip items without repair/replacement action or with "None" action
            if (string.IsNullOrEmpty(itemResult.RepairReplacementAction) ||
                itemResult.RepairReplacementAction == "None" ||
                itemResult.TaskCreated)
                continue;

            var checklistItem = itemResult.ChecklistItem;
            if (checklistItem == null || !checklistItem.AllowRepairReplacement)
                continue;

            // Determine estimated hours based on action
            var estimatedHours = itemResult.RepairReplacementAction == "Repair"
                ? checklistItem.EstimatedRepairHours
                : checklistItem.EstimatedReplacementHours;

            // Create descriptive task name and description
            var actionType = itemResult.RepairReplacementAction == "Repair" ? "REPAIR" : "REPLACE";

            // Create the work order task
            var task = new WorkOrderTask
            {
                Id = Guid.NewGuid(),
                WorkOrderId = workOrderId,
                TaskName = $"[{actionType}] {checklistItem.ItemName}",
                Description = $"[From Admission Checklist - {actionType}]\n" +
                              $"Item: {checklistItem.ItemName}\n" +
                              $"Description: {checklistItem.Description ?? "N/A"}\n" +
                              $"Action Required: {itemResult.RepairReplacementAction}\n" +
                              $"Inspector Comment: {itemResult.Comment ?? "No comment provided"}",
                Sequence = nextSequence++,
                EstimatedHours = estimatedHours,
                IsRequired = true,
                TenantId = TenantId,
                CreatedAt = DateTime.UtcNow,
                CreatedById = UserId,
                Status = "Pending"
            };

            await _taskRepository.AddAsync(task);

            // Mark the item result as having a task created
            itemResult.TaskCreated = true;
            itemResult.CreatedTaskId = task.Id;
            await _repository.UpdateItemResultAsync(itemResult);

            tasksCreated++;
        }

        if (tasksCreated > 0)
        {
            await _unitOfWork.SaveChangesAsync();
        }

        return tasksCreated;
    }

    #endregion

    #region Private Helper Methods

    private async Task UnsetDefaultForAssetCategoryAsync(Guid assetCategoryId, Guid? excludeTemplateId)
    {
        var templates = await _repository.GetTemplatesByAssetCategoryAsync(assetCategoryId, TenantId);
        foreach (var template in templates.Where(t => t.IsDefault && t.Id != excludeTemplateId))
        {
            template.IsDefault = false;
            await _repository.UpdateTemplateAsync(template);
        }
    }

    private PreInspectionChecklistItem CreateChecklistItem(Guid templateId, CreateAssetConditionItemDto dto)
    {
        return new PreInspectionChecklistItem
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            TemplateId = templateId,
            ItemName = dto.ItemName,
            Description = dto.Description,
            Category = dto.Category,
            ItemType = dto.ItemType,
            IsRequired = dto.IsRequired,
            SortOrder = dto.SortOrder,
            ChoiceOptions = dto.ChoiceOptions != null ? JsonSerializer.Serialize(dto.ChoiceOptions) : null,
            Unit = dto.Unit,
            MinValue = dto.MinValue,
            MaxValue = dto.MaxValue,
            DefaultValue = dto.DefaultValue,
            HelpText = dto.HelpText,
            RequiresPhoto = dto.RequiresPhoto,
            AllowRepairReplacement = dto.AllowRepairReplacement,
            DefaultRepairReplacementAction = dto.DefaultRepairReplacementAction,
            EstimatedRepairHours = dto.EstimatedRepairHours,
            EstimatedReplacementHours = dto.EstimatedReplacementHours,
            CreatedAt = DateTime.UtcNow,
            CreatedById = UserId
        };
    }

    private void UpdateChecklistItem(PreInspectionChecklistItem item, UpdateAssetConditionItemDto dto)
    {
        item.ItemName = dto.ItemName;
        item.Description = dto.Description;
        item.Category = dto.Category;
        item.ItemType = dto.ItemType;
        item.IsRequired = dto.IsRequired;
        item.SortOrder = dto.SortOrder;
        item.ChoiceOptions = dto.ChoiceOptions != null ? JsonSerializer.Serialize(dto.ChoiceOptions) : null;
        item.Unit = dto.Unit;
        item.MinValue = dto.MinValue;
        item.MaxValue = dto.MaxValue;
        item.DefaultValue = dto.DefaultValue;
        item.HelpText = dto.HelpText;
        item.RequiresPhoto = dto.RequiresPhoto;
        item.AllowRepairReplacement = dto.AllowRepairReplacement;
        item.DefaultRepairReplacementAction = dto.DefaultRepairReplacementAction;
        item.EstimatedRepairHours = dto.EstimatedRepairHours;
        item.EstimatedReplacementHours = dto.EstimatedReplacementHours;
        item.UpdatedAt = DateTime.UtcNow;
        item.LastModifiedById = UserId;
    }

    private static AssetConditionChecklistTemplateDto MapTemplateToDto(PreInspectionChecklistTemplate template)
    {
        return new AssetConditionChecklistTemplateDto
        {
            Id = template.Id,
            Name = template.Name,
            Description = template.Description,
            Category = template.Category,
            AssetCategoryId = template.AssetCategoryId,
            AssetCategoryName = template.AssetCategory?.Name ?? string.Empty,
            IsActive = template.IsActive,
            IsDefault = template.IsDefault,
            SortOrder = template.SortOrder,
            Version = template.Version,
            VersionNotes = template.VersionNotes,
            ItemCount = template.ChecklistItems?.Count ?? 0,
            CreatedAt = template.CreatedAt,
            ChecklistItems = template.ChecklistItems?.Select(MapItemToDto).ToList() ?? new List<AssetConditionChecklistItemDto>()
        };
    }

    private static AssetConditionChecklistItemDto MapItemToDto(PreInspectionChecklistItem item)
    {
        return new AssetConditionChecklistItemDto
        {
            Id = item.Id,
            TemplateId = item.TemplateId,
            ItemName = item.ItemName,
            Description = item.Description,
            Category = item.Category,
            ItemType = item.ItemType,
            IsRequired = item.IsRequired,
            SortOrder = item.SortOrder,
            ChoiceOptions = !string.IsNullOrEmpty(item.ChoiceOptions)
                ? JsonSerializer.Deserialize<List<string>>(item.ChoiceOptions)
                : null,
            Unit = item.Unit,
            MinValue = item.MinValue,
            MaxValue = item.MaxValue,
            DefaultValue = item.DefaultValue,
            HelpText = item.HelpText,
            RequiresPhoto = item.RequiresPhoto,
            AllowRepairReplacement = item.AllowRepairReplacement,
            DefaultRepairReplacementAction = item.DefaultRepairReplacementAction,
            EstimatedRepairHours = item.EstimatedRepairHours,
            EstimatedReplacementHours = item.EstimatedReplacementHours
        };
    }

    private async Task<AssetConditionRecordDto> MapRecordToDtoAsync(AssetConditionRecord record)
    {
        var itemResults = record.ItemResults?.ToList() ?? new List<AssetConditionItemResult>();
        var inspectorName = await GetUserNameByIdAsync(record.InspectorId);
        return new AssetConditionRecordDto
        {
            Id = record.Id,
            InspectionNumber = record.InspectionNumber,
            AssetId = record.AssetId,
            AssetName = record.Asset?.Name ?? string.Empty,
            AssetNumber = record.Asset?.AssetNumber ?? string.Empty,
            TemplateId = record.TemplateId,
            TemplateName = record.Template?.Name ?? string.Empty,
            InspectorId = record.InspectorId,
            InspectorName = inspectorName,
            InspectionDate = record.InspectionDate,
            InspectionType = record.InspectionType,
            Status = record.Status,
            GeneralNotes = record.GeneralNotes,
            AdmissionId = record.AdmissionId,
            DischargeId = record.DischargeId,
            PhotoPaths = !string.IsNullOrEmpty(record.PhotoPaths)
                ? JsonSerializer.Deserialize<List<string>>(record.PhotoPaths)
                : null,
            CreatedAt = record.CreatedAt,
            ItemResults = itemResults.Select(MapItemResultToDto).ToList(),
            TotalItems = itemResults.Count,
            CompletedItems = itemResults.Count(ir => ir.IsPresent.HasValue || !string.IsNullOrEmpty(ir.TextValue) || ir.NumericValue.HasValue || !string.IsNullOrEmpty(ir.SelectedOption))
        };
    }

    private async Task<AssetConditionRecordSummaryDto> MapRecordToSummaryDtoAsync(AssetConditionRecord record)
    {
        var itemResults = record.ItemResults?.ToList() ?? new List<AssetConditionItemResult>();
        var inspectorName = await GetUserNameByIdAsync(record.InspectorId);
        return new AssetConditionRecordSummaryDto
        {
            Id = record.Id,
            InspectionNumber = record.InspectionNumber,
            AssetName = record.Asset?.Name ?? string.Empty,
            AssetNumber = record.Asset?.AssetNumber ?? string.Empty,
            TemplateName = record.Template?.Name ?? string.Empty,
            InspectorName = inspectorName,
            InspectionDate = record.InspectionDate,
            InspectionType = record.InspectionType,
            Status = record.Status,
            TotalItems = itemResults.Count,
            CompletedItems = itemResults.Count(ir => ir.IsPresent.HasValue || !string.IsNullOrEmpty(ir.TextValue) || ir.NumericValue.HasValue || !string.IsNullOrEmpty(ir.SelectedOption)),
            HasAdmission = record.AdmissionId.HasValue,
            HasDischarge = record.DischargeId.HasValue
        };
    }

    private async Task<string> GetUserNameByIdAsync(Guid userId)
    {
        try
        {
            var user = await _userService.GetUserByIdAsync(userId);
            if (user != null)
            {
                // Return full name if available, otherwise username
                if (!string.IsNullOrEmpty(user.FirstName) || !string.IsNullOrEmpty(user.LastName))
                {
                    return $"{user.FirstName} {user.LastName}".Trim();
                }
                return user.UserName ?? "Unknown";
            }
            return "Unknown";
        }
        catch
        {
            return "Unknown";
        }
    }

    private static AssetConditionItemResultDto MapItemResultToDto(AssetConditionItemResult result)
    {
        return new AssetConditionItemResultDto
        {
            Id = result.Id,
            ConditionRecordId = result.ConditionRecordId,
            ChecklistItemId = result.ChecklistItemId,
            ItemName = result.ChecklistItem?.ItemName ?? string.Empty,
            Category = result.ChecklistItem?.Category ?? string.Empty,
            ItemType = result.ChecklistItem?.ItemType ?? "Boolean",
            IsRequired = result.ChecklistItem?.IsRequired ?? false,
            IsPresent = result.IsPresent,
            TextValue = result.TextValue,
            NumericValue = result.NumericValue,
            SelectedOption = result.SelectedOption,
            Comment = result.Comment,
            PhotoPaths = !string.IsNullOrEmpty(result.PhotoPaths)
                ? JsonSerializer.Deserialize<List<string>>(result.PhotoPaths)
                : null,
            InspectedAt = result.InspectedAt,
            RepairReplacementAction = result.RepairReplacementAction,
            TaskCreated = result.TaskCreated,
            CreatedTaskId = result.CreatedTaskId,
            AllowRepairReplacement = result.ChecklistItem?.AllowRepairReplacement ?? false
        };
    }

    #endregion
}
