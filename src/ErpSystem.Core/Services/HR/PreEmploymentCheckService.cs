using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

public class PreEmploymentCheckService : IPreEmploymentCheckService
{
    private readonly IPreEmploymentCheckRepository _checkRepository;
    private readonly IPreEmploymentCheckItemRepository _itemRepository;
    private readonly IReferenceCheckResponseRepository _referenceResponseRepository;
    private readonly IJobOfferRepository _offerRepository;
    private readonly IPreEmploymentCheckTemplateRepository _templateRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PreEmploymentCheckService> _logger;

    public PreEmploymentCheckService(
        IPreEmploymentCheckRepository checkRepository,
        IPreEmploymentCheckItemRepository itemRepository,
        IReferenceCheckResponseRepository referenceResponseRepository,
        IJobOfferRepository offerRepository,
        IPreEmploymentCheckTemplateRepository templateRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<PreEmploymentCheckService> logger)
    {
        _checkRepository = checkRepository;
        _itemRepository = itemRepository;
        _referenceResponseRepository = referenceResponseRepository;
        _offerRepository = offerRepository;
        _templateRepository = templateRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
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

    // A check owned by another tenant is reported as missing rather than forbidden, so the endpoints do
    // not confirm that the id exists elsewhere.
    private async Task<PreEmploymentCheck> GetOwnedCheckAsync(Guid id)
    {
        var entity = await _checkRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Pre-employment check with ID '{id}' not found.");
        return entity;
    }

    private async Task<PreEmploymentCheckItem> GetOwnedItemAsync(Guid id)
    {
        var entity = await _itemRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Pre-employment check item with ID '{id}' not found.");
        return entity;
    }

    private async Task<ReferenceCheckResponse> GetOwnedReferenceResponseAsync(Guid id)
    {
        var entity = await _referenceResponseRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Reference response with ID '{id}' not found.");
        return entity;
    }

    // ── Queries ──────────────────────────────────────────────────────────────

    public async Task<PreEmploymentCheckDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedCheckAsync(id);
        return entity.ToDto();
    }

    public async Task<PreEmploymentCheckDto?> GetByOfferIdAsync(Guid offerId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _checkRepository.GetByOfferIdAsync(offerId);
        return entity != null && entity.TenantId == tenantId ? entity.ToDto() : null;
    }

    public async Task<PreEmploymentCheckDetailDto> GetWithItemsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _checkRepository.GetWithItemsAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Pre-employment check with ID '{id}' not found.");
        return entity.ToDetailDto();
    }

    public async Task<IEnumerable<PreEmploymentCheckDto>> GetByStatusAsync(PreEmploymentCheckStatus status, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _checkRepository.GetByStatusAsync(status);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto());
    }

    // ── CRUD ──────────────────────────────────────────────────────────────────

    public async Task<PreEmploymentCheckDto> CreateAsync(CreatePreEmploymentCheckDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        var entity = createDto.ToEntity(current, createdByUserId);
        entity.OverallStatus = PreEmploymentCheckStatus.Pending;

        var offer = await _offerRepository.GetByIdAsync(createDto.JobOfferId);
        if (offer == null || offer.TenantId != current)
            throw new ArgumentException($"Job offer '{createDto.JobOfferId}' not found.");

        await _checkRepository.AddAsync(entity);

        // Create default check items from template if provided
        foreach (var itemDto in createDto.Items)
        {
            itemDto.PreEmploymentCheckId = entity.Id;
            var item = itemDto.ToEntity(current, createdByUserId);
            await _itemRepository.AddAsync(item);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Pre-employment check created for offer {OfferId}", createDto.JobOfferId);
        return entity.ToDto();
    }

    // ── Check items ───────────────────────────────────────────────────────────

    public async Task<IEnumerable<PreEmploymentCheckItemDto>> GetItemsAsync(Guid checkId, CancellationToken cancellationToken = default)
    {
        await GetOwnedCheckAsync(checkId);
        var tenantId = GetTenantId();
        var entities = await _itemRepository.GetByPreEmploymentCheckIdAsync(checkId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto());
    }

    public async Task<PreEmploymentCheckItemDto> AddItemAsync(CreatePreEmploymentCheckItemDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        await GetOwnedCheckAsync(createDto.PreEmploymentCheckId);

        var entity = createDto.ToEntity(current, createdByUserId);
        await _itemRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<PreEmploymentCheckItemDto> UpdateItemAsync(UpdatePreEmploymentCheckItemDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedItemAsync(updateDto.Id);

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _itemRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteItemAsync(Guid itemId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedItemAsync(itemId);

        await _itemRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IEnumerable<PreEmploymentCheckItemDto>> GetBlockingFailuresAsync(Guid checkId, CancellationToken cancellationToken = default)
    {
        await GetOwnedCheckAsync(checkId);
        var tenantId = GetTenantId();
        var entities = await _itemRepository.GetBlockingFailuresAsync(checkId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto());
    }

    // ── Reference responses ───────────────────────────────────────────────────

    public async Task<ReferenceCheckResponseDto> AddReferenceResponseAsync(CreateReferenceCheckResponseDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        await GetOwnedItemAsync(createDto.CheckItemId);

        // Upsert: the unique index allows only one response per check item.
        // If a response already exists, update it in place.
        var existing = (await _referenceResponseRepository.GetByCheckItemIdAsync(createDto.CheckItemId))
            .FirstOrDefault(r => r.TenantId == current);

        if (existing != null)
        {
            existing.RefereeName                = createDto.RefereeName;
            existing.RefereeOrganisation        = createDto.RefereeOrganisation;
            existing.RefereePosition            = createDto.RefereePosition;
            existing.RefereeEmail               = createDto.RefereeEmail;
            existing.RefereePhone               = createDto.RefereePhone;
            existing.ResponseMethod             = createDto.ResponseMethod;
            existing.OverallRating              = createDto.OverallRating;
            existing.Comments                   = createDto.Comments;
            existing.WouldRehire                = createDto.WouldRehire;
            existing.ConfirmedDatesOfEmployment  = createDto.ConfirmedDatesOfEmployment;
            existing.ConfirmedPositionHeld       = createDto.ConfirmedPositionHeld;
            existing.ConfirmedReasonForLeaving   = createDto.ConfirmedReasonForLeaving;
            existing.DocumentPath               = createDto.DocumentPath;
            existing.UpdatedAt                  = DateTime.UtcNow;
            existing.UpdatedBy                  = createdByUserId.ToString();
            await _referenceResponseRepository.UpdateAsync(existing);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return existing.ToDto();
        }

        var entity = createDto.ToEntity(current, createdByUserId);
        await _referenceResponseRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<ReferenceCheckResponseDto>> GetReferenceResponsesAsync(Guid checkItemId, CancellationToken cancellationToken = default)
    {
        await GetOwnedItemAsync(checkItemId);
        var tenantId = GetTenantId();
        var entities = await _referenceResponseRepository.GetByCheckItemIdAsync(checkItemId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto());
    }

    public async Task<ReferenceCheckResponseDto> UpdateReferenceResponseAsync(UpdateReferenceCheckResponseDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedReferenceResponseAsync(updateDto.Id);

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _referenceResponseRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteReferenceResponseAsync(Guid referenceResponseId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedReferenceResponseAsync(referenceResponseId);

        await _referenceResponseRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Workflow ──────────────────────────────────────────────────────────────

    public async Task<bool> CompleteCheckAsync(Guid checkId, Guid completedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _checkRepository.GetWithItemsAsync(checkId);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Pre-employment check with ID '{checkId}' not found.");

        var hasBlockingFailures = entity.Items.Any(i => i.IsBlockingOnFail && i.Passed != true);
        entity.OverallStatus = hasBlockingFailures ? PreEmploymentCheckStatus.Failed : PreEmploymentCheckStatus.Completed;
        entity.CompletedDate = DateTime.UtcNow;

        await _checkRepository.UpdateAsync(entity);

        // When all blocking checks pass, advance the linked offer to ChecksCleared
        if (!hasBlockingFailures)
        {
            var offer = await _offerRepository.GetByIdAsync(entity.JobOfferId);
            if (offer != null && offer.TenantId == entity.TenantId && offer.OfferStatus == JobOfferStatus.ConditionallyAccepted)
            {
                offer.OfferStatus = JobOfferStatus.ChecksCleared;
                await _offerRepository.UpdateAsync(offer);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Pre-employment check {CheckId} completed with status {Status}", checkId, entity.OverallStatus);
        return true;
    }

    public async Task<IEnumerable<PreEmploymentCheckItemDto>> GetItemsByStatusAsync(CheckItemStatus status, Guid? checkId = null, CancellationToken cancellationToken = default)
    {
        if (checkId.HasValue)
            await GetOwnedCheckAsync(checkId.Value);

        var tenantId = GetTenantId();
        var entities = await _itemRepository.GetByStatusAsync(status, checkId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto());
    }

    public async Task<IEnumerable<PreEmploymentCheckItemDto>> GetMandatoryItemsAsync(Guid checkId, CancellationToken cancellationToken = default)
    {
        await GetOwnedCheckAsync(checkId);
        var tenantId = GetTenantId();
        var entities = await _itemRepository.GetMandatoryItemsAsync(checkId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto());
    }

    public async Task<IEnumerable<ReferenceCheckResponseDto>> GetReferenceResponsesByRefereeAsync(Guid refereeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _referenceResponseRepository.GetByRefereeIdAsync(refereeId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto());
    }

    // ── Template application ──────────────────────────────────────────────────

    public async Task<PreEmploymentCheckDetailDto> ApplyTemplateAsync(Guid checkId, Guid templateId, Guid appliedByUserId, bool overwriteExisting = false, CancellationToken ct = default)
    {
        var check = await _checkRepository.GetWithItemsAsync(checkId);
        if (check == null || check.TenantId != GetTenantId())
            throw new ArgumentException($"Pre-employment check '{checkId}' not found.");

        var template = await _templateRepository.GetByIdWithItemsAsync(templateId);
        if (template == null || template.TenantId != check.TenantId)
            throw new ArgumentException($"Pre-employment check template '{templateId}' not found.");

        var existingTypes = check.Items.Select(i => i.CheckType).ToHashSet();

        foreach (var templateItem in template.Items)
        {
            if (!overwriteExisting && existingTypes.Contains(templateItem.CheckType))
                continue;

            var newItem = new PreEmploymentCheckItem
            {
                TenantId = check.TenantId,
                PreEmploymentCheckId = check.Id,
                CheckType = templateItem.CheckType,
                ServiceProviderName = templateItem.DefaultServiceProvider,
                Instructions = templateItem.Instructions,
                IsMandatory = templateItem.IsMandatory,
                IsBlockingOnFail = templateItem.IsBlockingOnFail,
                ExpectedDays = templateItem.ExpectedDays,
                Status = CheckItemStatus.Pending,
                CreatedBy = appliedByUserId.ToString(),
            };

            await _itemRepository.AddAsync(newItem);
        }

        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Template {TemplateId} applied to pre-employment check {CheckId}", templateId, checkId);

        return (await _checkRepository.GetWithItemsAsync(checkId))!.ToDetailDto();
    }
}

// ============================================================================
// PRE-EMPLOYMENT CHECK TEMPLATE SERVICE
// ============================================================================

public class PreEmploymentCheckTemplateService : IPreEmploymentCheckTemplateService
{
    private readonly IPreEmploymentCheckTemplateRepository _templateRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PreEmploymentCheckTemplateService> _logger;

    public PreEmploymentCheckTemplateService(
        IPreEmploymentCheckTemplateRepository templateRepository,
        IUnitOfWork unitOfWork,
        ILogger<PreEmploymentCheckTemplateService> logger)
    {
        _templateRepository = templateRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<IEnumerable<PreEmploymentCheckTemplateDto>> GetAllAsync(CancellationToken ct = default)
    {
        var entities = await _templateRepository.GetAllActiveAsync();
        return entities.Select(e => e.ToDto());
    }

    public async Task<PreEmploymentCheckTemplateDetailDto> GetWithItemsAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _templateRepository.GetByIdWithItemsAsync(id)
            ?? throw new ArgumentException($"Pre-employment check template '{id}' not found.");
        return entity.ToDetailDto();
    }

    public async Task<PreEmploymentCheckTemplateDetailDto> CreateAsync(CreatePreEmploymentCheckTemplateDto dto, Guid tenantId, Guid createdByUserId, CancellationToken ct = default)
    {
        var entity = dto.ToEntity(tenantId, createdByUserId);
        await _templateRepository.AddAsync(entity);

        foreach (var itemDto in dto.Items)
        {
            itemDto.TemplateId = entity.Id;
            var item = itemDto.ToEntity(tenantId, createdByUserId);
            entity.Items.Add(item);
        }

        await _unitOfWork.SaveChangesAsync(ct);
        _logger.LogInformation("Pre-employment check template '{Name}' created", entity.Name);

        return (await _templateRepository.GetByIdWithItemsAsync(entity.Id))!.ToDetailDto();
    }

    public async Task<PreEmploymentCheckTemplateDto> UpdateAsync(UpdatePreEmploymentCheckTemplateDto dto, Guid updatedByUserId, CancellationToken ct = default)
    {
        var entity = await _templateRepository.GetByIdAsync(dto.Id)
            ?? throw new ArgumentException($"Pre-employment check template '{dto.Id}' not found.");

        entity.UpdateEntity(dto, updatedByUserId);
        await _templateRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _templateRepository.GetByIdAsync(id)
            ?? throw new ArgumentException($"Pre-employment check template '{id}' not found.");

        await _templateRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }

    public async Task<PreEmploymentCheckTemplateItemDto> AddItemAsync(CreatePreEmploymentCheckTemplateItemDto dto, Guid tenantId, Guid createdByUserId, CancellationToken ct = default)
    {
        var template = await _templateRepository.GetByIdWithItemsAsync(dto.TemplateId)
            ?? throw new ArgumentException($"Pre-employment check template '{dto.TemplateId}' not found.");

        var item = dto.ToEntity(tenantId, createdByUserId);
        await _templateRepository.AddTemplateItemAsync(item);
        await _unitOfWork.SaveChangesAsync(ct);
        return item.ToDto();
    }

    public async Task<PreEmploymentCheckTemplateItemDto> UpdateItemAsync(UpdatePreEmploymentCheckTemplateItemDto dto, Guid updatedByUserId, CancellationToken ct = default)
    {
        var template = await _templateRepository.GetByIdWithItemsAsync(dto.TemplateId)
            ?? throw new ArgumentException($"Pre-employment check template '{dto.TemplateId}' not found.");

        var item = template.Items.FirstOrDefault(i => i.Id == dto.Id)
            ?? throw new ArgumentException($"Template item '{dto.Id}' not found on template '{dto.TemplateId}'.");

        item.UpdateEntity(dto, updatedByUserId);
        await _templateRepository.UpdateAsync(template);
        await _unitOfWork.SaveChangesAsync(ct);
        return item.ToDto();
    }

    public async Task<bool> DeleteItemAsync(Guid itemId, CancellationToken ct = default)
    {
        // Load all templates with items to locate the item
        var templates = await _templateRepository.GetAllActiveAsync();
        var item = templates.SelectMany(t => t.Items).FirstOrDefault(i => i.Id == itemId);

        if (item == null)
            throw new ArgumentException($"Template item '{itemId}' not found.");

        item.IsDeleted = true;
        item.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }
}
