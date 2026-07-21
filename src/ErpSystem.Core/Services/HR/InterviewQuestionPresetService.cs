using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

public class InterviewQuestionPresetService : IInterviewQuestionPresetService
{
    private readonly IInterviewQuestionPresetRepository _presetRepository;
    private readonly IInterviewQuestionPresetItemRepository _itemRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<InterviewQuestionPresetService> _logger;

    public InterviewQuestionPresetService(
        IInterviewQuestionPresetRepository presetRepository,
        IInterviewQuestionPresetItemRepository itemRepository,
        IUnitOfWork unitOfWork,
        ILogger<InterviewQuestionPresetService> logger)
    {
        _presetRepository = presetRepository;
        _itemRepository = itemRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<IEnumerable<InterviewQuestionPresetSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _presetRepository.GetAllWithItemsAsync();
        return entities.Select(e => e.ToSummaryDto());
    }

    public async Task<InterviewQuestionPresetDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _presetRepository.GetWithItemsAsync(id);
        if (entity == null)
            throw new ArgumentException($"Interview question preset with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<InterviewQuestionPresetDto> CreateAsync(
        CreateInterviewQuestionPresetDto createDto,
        Guid tenantId,
        Guid createdByUserId,
        CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);

        foreach (var itemDto in createDto.Items)
        {
            var item = itemDto.ToEntity(tenantId, createdByUserId);
            entity.Items.Add(item);
        }

        await _presetRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        var created = await _presetRepository.GetWithItemsAsync(entity.Id);
        return created!.ToDto();
    }

    public async Task<InterviewQuestionPresetDto> UpdateAsync(
        UpdateInterviewQuestionPresetDto updateDto,
        Guid updatedByUserId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _presetRepository.GetByIdAsync(updateDto.Id);
        if (entity == null)
            throw new ArgumentException($"Interview question preset with ID '{updateDto.Id}' not found.");

        entity.UpdateEntity(updateDto, updatedByUserId);

        // Sync items when provided
        if (updateDto.Items is not null)
        {
            var existingItems = (await _itemRepository.GetByPresetIdAsync(entity.Id)).ToList();

            // Delete items removed from the list
            var incomingIds = updateDto.Items
                .Where(i => i.Id != Guid.Empty)
                .Select(i => i.Id)
                .ToHashSet();

            foreach (var removed in existingItems.Where(e => !incomingIds.Contains(e.Id)))
                await _itemRepository.DeleteAsync(removed.Id);

            // Update existing or insert new
            foreach (var itemDto in updateDto.Items)
            {
                if (itemDto.Id != Guid.Empty)
                {
                    var existing = existingItems.FirstOrDefault(e => e.Id == itemDto.Id);
                    if (existing is not null)
                    {
                        existing.QuestionTypeId        = itemDto.QuestionTypeId;
                        existing.RequiredQuestionCount = itemDto.RequiredQuestionCount;
                        existing.AllowedPoolSize        = itemDto.AllowedPoolSize;
                        existing.DisplayOrder          = itemDto.DisplayOrder;
                        existing.UpdatedAt             = DateTime.UtcNow;
                        existing.UpdatedBy             = updatedByUserId.ToString();
                        await _itemRepository.UpdateAsync(existing);
                    }
                }
                else
                {
                    var newItem = new InterviewQuestionPresetItem
                    {
                        TenantId              = entity.TenantId,
                        PresetId              = entity.Id,
                        QuestionTypeId        = itemDto.QuestionTypeId,
                        RequiredQuestionCount = itemDto.RequiredQuestionCount,
                        AllowedPoolSize       = itemDto.AllowedPoolSize,
                        DisplayOrder          = itemDto.DisplayOrder,
                        CreatedBy             = updatedByUserId.ToString(),
                    };
                    await _itemRepository.AddAsync(newItem);
                }
            }
        }

        await _unitOfWork.SaveChangesAsync();

        var updated = await _presetRepository.GetWithItemsAsync(entity.Id);
        return updated!.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _presetRepository.GetByIdAsync(id);
        if (entity == null) return false;

        await _presetRepository.DeleteAsync(id);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    public async Task<InterviewQuestionPresetItemDto> AddItemAsync(
        CreateInterviewQuestionPresetItemDto createDto,
        Guid tenantId,
        Guid createdByUserId,
        CancellationToken cancellationToken = default)
    {
        var item = createDto.ToEntity(tenantId, createdByUserId);
        await _itemRepository.AddAsync(item);
        await _unitOfWork.SaveChangesAsync();

        var items = await _itemRepository.GetByPresetIdAsync(item.PresetId);
        var saved = items.FirstOrDefault(i => i.Id == item.Id);
        return saved!.ToDto();
    }

    public async Task<InterviewQuestionPresetItemDto> UpdateItemAsync(
        UpdateInterviewQuestionPresetItemDto updateDto,
        Guid updatedByUserId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _itemRepository.GetByIdAsync(updateDto.Id);
        if (entity == null)
            throw new ArgumentException($"Preset item with ID '{updateDto.Id}' not found.");

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _unitOfWork.SaveChangesAsync();

        var items = await _itemRepository.GetByPresetIdAsync(entity.PresetId);
        var updated = items.FirstOrDefault(i => i.Id == entity.Id);
        return updated!.ToDto();
    }

    public async Task<bool> DeleteItemAsync(Guid itemId, CancellationToken cancellationToken = default)
    {
        var entity = await _itemRepository.GetByIdAsync(itemId);
        if (entity == null) return false;

        await _itemRepository.DeleteAsync(itemId);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }
}
