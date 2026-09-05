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
    private readonly IJobInterviewQuestionTypeRepository _questionTypeRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<InterviewQuestionPresetService> _logger;

    public InterviewQuestionPresetService(
        IInterviewQuestionPresetRepository presetRepository,
        IInterviewQuestionPresetItemRepository itemRepository,
        IJobInterviewQuestionTypeRepository questionTypeRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<InterviewQuestionPresetService> logger)
    {
        _presetRepository = presetRepository;
        _itemRepository = itemRepository;
        _questionTypeRepository = questionTypeRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <summary>
    /// A preset item points at a question type; nothing validated that it existed or belonged to this
    /// tenant, so a preset could reference another tenant's type and then silently contribute an empty
    /// question plan to every interview scheduled from it.
    /// </summary>
    private async Task EnsureQuestionTypesOwnedAsync(IEnumerable<Guid> questionTypeIds, Guid tenantId)
    {
        foreach (var id in questionTypeIds.Distinct())
        {
            var type = await _questionTypeRepository.GetByIdAsync(id);
            if (type == null || type.TenantId != tenantId || type.IsDeleted)
                throw new ArgumentException($"Interview question type with ID '{id}' not found.");
        }
    }

    /// <summary>A preset that names the same question type twice would draw two plans for one section.</summary>
    private static void EnsureNoDuplicateTypes(IEnumerable<Guid> questionTypeIds)
    {
        var ids = questionTypeIds.ToList();
        if (ids.Distinct().Count() != ids.Count)
            throw new InvalidOperationException("A preset cannot list the same question type twice.");
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes reads/writes to
    // the current tenant explicitly.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    // A preset owned by another tenant is reported as missing rather than forbidden, so the endpoints do
    // not confirm that the id exists elsewhere.
    private async Task<InterviewQuestionPreset> GetOwnedPresetAsync(Guid id)
    {
        var entity = await _presetRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Interview question preset with ID '{id}' not found.");
        return entity;
    }

    private async Task<InterviewQuestionPresetItem> GetOwnedItemAsync(Guid id)
    {
        var entity = await _itemRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Preset item with ID '{id}' not found.");
        return entity;
    }

    public async Task<IEnumerable<InterviewQuestionPresetSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _presetRepository.GetAllWithItemsAsync();
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToSummaryDto());
    }

    public async Task<InterviewQuestionPresetDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _presetRepository.GetWithItemsAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Interview question preset with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<InterviewQuestionPresetDto> CreateAsync(
        CreateInterviewQuestionPresetDto createDto,
        Guid tenantId,
        Guid createdByUserId,
        CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        EnsureNoDuplicateTypes(createDto.Items.Select(i => i.QuestionTypeId));
        await EnsureQuestionTypesOwnedAsync(createDto.Items.Select(i => i.QuestionTypeId), current);

        var entity = createDto.ToEntity(current, createdByUserId);

        foreach (var itemDto in createDto.Items)
        {
            var item = itemDto.ToEntity(current, createdByUserId);
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
        var entity = await GetOwnedPresetAsync(updateDto.Id);

        entity.UpdateEntity(updateDto, updatedByUserId);

        // Sync items when provided. ⚠ This is a replace-set payload: omitting an item deletes it.
        if (updateDto.Items is not null)
        {
            EnsureNoDuplicateTypes(updateDto.Items.Select(i => i.QuestionTypeId));
            await EnsureQuestionTypesOwnedAsync(updateDto.Items.Select(i => i.QuestionTypeId), entity.TenantId);

            var existingItems = (await _itemRepository.GetByPresetIdAsync(entity.Id))
                .Where(i => i.TenantId == entity.TenantId)
                .ToList();

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
        var entity = await GetOwnedPresetAsync(id);

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
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        await GetOwnedPresetAsync(createDto.PresetId);
        await EnsureQuestionTypesOwnedAsync(new[] { createDto.QuestionTypeId }, current);

        var siblings = (await _itemRepository.GetByPresetIdAsync(createDto.PresetId))
            .Where(i => i.TenantId == current)
            .ToList();
        if (siblings.Any(i => i.QuestionTypeId == createDto.QuestionTypeId))
            throw new InvalidOperationException("That question type is already in this preset.");

        var item = createDto.ToEntity(current, createdByUserId);
        await _itemRepository.AddAsync(item);
        await _unitOfWork.SaveChangesAsync();

        var items = await _itemRepository.GetByPresetIdAsync(item.PresetId);
        var saved = items.FirstOrDefault(i => i.Id == item.Id && i.TenantId == current);
        return saved!.ToDto();
    }

    public async Task<InterviewQuestionPresetItemDto> UpdateItemAsync(
        UpdateInterviewQuestionPresetItemDto updateDto,
        Guid updatedByUserId,
        CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedItemAsync(updateDto.Id);
        await EnsureQuestionTypesOwnedAsync(new[] { updateDto.QuestionTypeId }, entity.TenantId);

        var siblings = (await _itemRepository.GetByPresetIdAsync(entity.PresetId))
            .Where(i => i.TenantId == entity.TenantId && i.Id != entity.Id)
            .ToList();
        if (siblings.Any(i => i.QuestionTypeId == updateDto.QuestionTypeId))
            throw new InvalidOperationException("That question type is already in this preset.");

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _unitOfWork.SaveChangesAsync();

        var items = await _itemRepository.GetByPresetIdAsync(entity.PresetId);
        var updated = items.FirstOrDefault(i => i.Id == entity.Id && i.TenantId == entity.TenantId);
        return updated!.ToDto();
    }

    public async Task<bool> DeleteItemAsync(Guid itemId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedItemAsync(itemId);

        await _itemRepository.DeleteAsync(itemId);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }
}
