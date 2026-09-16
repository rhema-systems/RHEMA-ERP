using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

public class PreEmploymentCheckService : IPreEmploymentCheckService
{
    private readonly IPreEmploymentCheckRepository _checkRepository;
    private readonly IPreEmploymentCheckItemRepository _itemRepository;
    private readonly IReferenceCheckResponseRepository _referenceResponseRepository;
    private readonly IJobOfferRepository _offerRepository;
    private readonly IPreEmploymentCheckTemplateRepository _templateRepository;
    private readonly IGenericRepository<PreEmploymentCheckProviderService> _providerRepository;
    private readonly ErpSystem.Core.Interfaces.Procurement.ISupplierRepository _suppliers;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PreEmploymentCheckService> _logger;

    public PreEmploymentCheckService(
        IPreEmploymentCheckRepository checkRepository,
        IPreEmploymentCheckItemRepository itemRepository,
        IReferenceCheckResponseRepository referenceResponseRepository,
        IJobOfferRepository offerRepository,
        IPreEmploymentCheckTemplateRepository templateRepository,
        IGenericRepository<PreEmploymentCheckProviderService> providerRepository,
        ErpSystem.Core.Interfaces.Procurement.ISupplierRepository suppliers,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<PreEmploymentCheckService> logger)
    {
        _checkRepository = checkRepository;
        _itemRepository = itemRepository;
        _referenceResponseRepository = referenceResponseRepository;
        _offerRepository = offerRepository;
        _templateRepository = templateRepository;
        _providerRepository = providerRepository;
        _suppliers = suppliers;
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
        createDto.ServiceProviderName = await ResolveProviderNameAsync(createDto.ServiceProviderSupplierId, createDto.ServiceProviderName, current);

        var entity = createDto.ToEntity(current, createdByUserId);
        await _itemRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<PreEmploymentCheckItemDto> UpdateItemAsync(UpdatePreEmploymentCheckItemDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedItemAsync(updateDto.Id);
        updateDto.ServiceProviderName = await ResolveProviderNameAsync(updateDto.ServiceProviderSupplierId, updateDto.ServiceProviderName, GetTenantId());

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _itemRepository.UpdateAsync(entity);
        await PromoteCheckToInProgressAsync(entity.PreEmploymentCheckId, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    /// <summary>
    /// Moves a check set from <c>Pending</c> to <c>InProgress</c> once any of its items has been
    /// started (G-11.1).
    /// </summary>
    /// <remarks>
    /// <para>⚠ <b><c>PreEmploymentCheckStatus.InProgress</c> was written by nothing.</b>
    /// <c>OverallStatus</c> had exactly three writers: <c>Pending</c> at creation, and
    /// <c>Completed</c> or <c>Failed</c> from <c>CompleteAsync</c>. Nothing ever wrote
    /// <c>InProgress</c>, <c>CompletedWithCaution</c> or <c>Waived</c>.</para>
    ///
    /// <para>The clearance queue screen <b>defaults to In Progress</b>, and its own comment explains
    /// the choice: *"Pending/Completed/Failed/Waived read as 'nothing to chase' or 'already
    /// resolved'."* The one status it picked as the live queue was the one status the application
    /// never set — so on a real tenant that screen opened permanently empty, and three of its six
    /// dropdown options could never match a row. The only writer of those three values anywhere in
    /// the solution was <c>TdcDemoRecruitmentHistorySeeder</c>, assigning them by a modulo, so the
    /// demo tenant was populated and convincing. Appendix C's fourth pattern at its sharpest: a
    /// whole screen that works in a demo and is inert in production.</para>
    ///
    /// <para>What the screen means to show — a check set started but not finished — was real and
    /// identifiable all along (<c>Pending</c> with some item past <c>Pending</c>); there was simply
    /// no status recording it. There is now, and it is written where the transition actually
    /// happens rather than derived at read time, so every reader agrees.</para>
    ///
    /// <para>Deliberately one-way and narrow: it only ever moves <c>Pending → InProgress</c>. It
    /// never touches a completed, failed or waived set, and never moves back — a check set does not
    /// become un-started because somebody reset one item.</para>
    /// </remarks>
    private async Task PromoteCheckToInProgressAsync(Guid checkId, CancellationToken cancellationToken)
    {
        var check = await _checkRepository.GetByIdAsync(checkId);
        if (check is null || check.TenantId != GetTenantId()) return;
        if (check.OverallStatus != PreEmploymentCheckStatus.Pending) return;

        var items = await _itemRepository.GetByPreEmploymentCheckIdAsync(checkId);
        var anyStarted = items.Any(i =>
            i.TenantId == check.TenantId &&
            !i.IsDeleted &&
            i.Status != CheckItemStatus.Pending);

        if (!anyStarted) return;

        check.OverallStatus = PreEmploymentCheckStatus.InProgress;
        check.UpdatedAt = DateTime.UtcNow;
        await _checkRepository.UpdateAsync(check);

        _logger.LogInformation(
            "Pre-employment check {CheckId} moved to InProgress — its first item has been started.",
            checkId);
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

    // ── Providers (round 3, lane G; D-14) ─────────────────────────────────────

    /// <summary>
    /// A named supplier must be the tenant's live Procurement supplier; its name is mirrored into
    /// the snapshot column so a renamed or retired supplier never rewrites a completed check.
    /// Without a supplier the typed name stands.
    /// </summary>
    private async Task<string?> ResolveProviderNameAsync(Guid? supplierId, string? typedName, Guid tenantId)
    {
        if (supplierId is not { } id) return string.IsNullOrWhiteSpace(typedName) ? null : typedName.Trim();
        var supplier = await _suppliers.GetQueryable().AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId && !x.IsDeleted);
        if (supplier is null)
            throw new InvalidOperationException("That service provider is not a supplier of this organisation. Pick one from the supplier register.");
        return supplier.Name;
    }

    public async Task<IEnumerable<PreEmploymentCheckProviderServiceDto>> GetProviderServicesAsync(PreEmploymentCheckType? checkType = null, bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var query = _providerRepository.GetQueryable().AsNoTracking()
            .Include(x => x.Supplier)
            .Where(x => x.TenantId == tenantId && !x.IsDeleted);
        if (checkType is { } type) query = query.Where(x => x.CheckType == type);
        if (!includeInactive) query = query.Where(x => x.IsActive && x.Supplier.IsActive);
        var rows = await query.OrderBy(x => x.Supplier.Name).ThenBy(x => x.CheckType).ToListAsync(cancellationToken);
        return rows.Select(ToProviderDto);
    }

    public async Task<IEnumerable<PreEmploymentCheckProviderServiceDto>> AddProviderServicesAsync(CreatePreEmploymentCheckProviderServicesDto dto, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var supplier = await _suppliers.GetQueryable().AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == dto.SupplierId && x.TenantId == tenantId && !x.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("That supplier is not on this organisation's register.");
        var types = dto.CheckTypes.Where(t => Enum.IsDefined(typeof(PreEmploymentCheckType), t)).Distinct().ToList();
        if (types.Count == 0)
            throw new InvalidOperationException("Say which checks the supplier provides.");

        var existing = (await _providerRepository.FindAsync(x => x.TenantId == tenantId && x.SupplierId == supplier.Id && !x.IsDeleted))
            .ToDictionary(x => x.CheckType);
        foreach (var type in types)
        {
            if (existing.TryGetValue(type, out var row))
            {
                if (!row.IsActive || row.Notes != dto.Notes)
                {
                    row.IsActive = true;
                    row.Notes = dto.Notes;
                    row.UpdatedAt = DateTime.UtcNow;
                    row.UpdatedBy = createdByUserId.ToString();
                    await _providerRepository.UpdateAsync(row);
                }
                continue;
            }
            await _providerRepository.AddAsync(new PreEmploymentCheckProviderService
            {
                TenantId = tenantId, SupplierId = supplier.Id, CheckType = type, Notes = dto.Notes, IsActive = true,
                CreatedBy = createdByUserId.ToString(),
            });
        }
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        var rows = await _providerRepository.GetQueryable().AsNoTracking().Include(x => x.Supplier)
            .Where(x => x.TenantId == tenantId && x.SupplierId == supplier.Id && !x.IsDeleted && types.Contains(x.CheckType))
            .OrderBy(x => x.CheckType).ToListAsync(cancellationToken);
        return rows.Select(ToProviderDto);
    }

    public async Task<bool> RemoveProviderServiceAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var row = await _providerRepository.GetByIdAsync(id);
        if (row is null || row.TenantId != tenantId || row.IsDeleted)
            throw new ArgumentException($"Provider service '{id}' not found.");
        await _providerRepository.DeleteAsync(row);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static PreEmploymentCheckProviderServiceDto ToProviderDto(PreEmploymentCheckProviderService x) => new()
    {
        Id = x.Id, TenantId = x.TenantId, CreatedAt = x.CreatedAt, CreatedBy = x.CreatedBy ?? string.Empty,
        UpdatedAt = x.UpdatedAt, UpdatedBy = x.UpdatedBy,
        SupplierId = x.SupplierId, SupplierCode = x.Supplier?.SupplierCode ?? string.Empty, SupplierName = x.Supplier?.Name ?? string.Empty,
        SupplierIsActive = x.Supplier?.IsActive ?? false, CheckType = x.CheckType, Notes = x.Notes, IsActive = x.IsActive,
    };

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
            // The document is not touched here: it arrives through the upload gate, and re-posting
            // the reference details must not clear evidence that has already been attached.
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

    // ── Evidence documents ────────────────────────────────────────────────────
    //
    // Both write paths take the ids the controlled-upload gate produced, never a path from the
    // caller. The gate is what scans the file and registers it in the DMS; the legacy DocumentPath
    // column is left readable so documents stored before this change still resolve.

    public async Task<PreEmploymentCheckItemDto> RecordItemDocumentAsync(
        Guid itemId, Guid fileUploadRecordId, Guid? documentRecordId, Guid? documentVersionId,
        string? fileName, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedItemAsync(itemId);

        entity.DocumentFileUploadRecordId = fileUploadRecordId;
        entity.DocumentRecordId           = documentRecordId;
        entity.DocumentVersionId          = documentVersionId;
        entity.DocumentFileName           = fileName;
        entity.UpdatedAt                  = DateTime.UtcNow;

        await _itemRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Evidence recorded for pre-employment check item {ItemId} ({CheckType}).",
            entity.Id, entity.CheckType);

        return entity.ToDto();
    }

    public async Task<ReferenceCheckResponseDto> RecordReferenceDocumentAsync(
        Guid responseId, Guid fileUploadRecordId, Guid? documentRecordId, Guid? documentVersionId,
        string? fileName, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedReferenceResponseAsync(responseId);

        entity.DocumentFileUploadRecordId = fileUploadRecordId;
        entity.DocumentRecordId           = documentRecordId;
        entity.DocumentVersionId          = documentVersionId;
        entity.DocumentFileName           = fileName;
        entity.UpdatedAt                  = DateTime.UtcNow;

        await _referenceResponseRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    /// <summary>
    /// The stored-document handles for a check item, for the download route. Returns null when the
    /// item carries no evidence at all.
    /// </summary>
    public async Task<PreEmploymentDocumentHandleDto?> GetItemDocumentHandleAsync(
        Guid itemId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedItemAsync(itemId);
        if (!entity.DocumentFileUploadRecordId.HasValue && string.IsNullOrWhiteSpace(entity.DocumentPath))
            return null;

        return new PreEmploymentDocumentHandleDto
        {
            FileUploadRecordId = entity.DocumentFileUploadRecordId,
            DocumentRecordId   = entity.DocumentRecordId,
            DocumentVersionId  = entity.DocumentVersionId,
            LegacyPath         = entity.DocumentPath,
            FileName           = entity.DocumentFileName ?? $"check-{entity.CheckType}.pdf",
        };
    }

    /// <summary>As above, for a written reference returned by a referee.</summary>
    public async Task<PreEmploymentDocumentHandleDto?> GetReferenceDocumentHandleAsync(
        Guid responseId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedReferenceResponseAsync(responseId);
        if (!entity.DocumentFileUploadRecordId.HasValue && string.IsNullOrWhiteSpace(entity.DocumentPath))
            return null;

        return new PreEmploymentDocumentHandleDto
        {
            FileUploadRecordId = entity.DocumentFileUploadRecordId,
            DocumentRecordId   = entity.DocumentRecordId,
            DocumentVersionId  = entity.DocumentVersionId,
            LegacyPath         = entity.DocumentPath,
            FileName           = entity.DocumentFileName ?? "reference.pdf",
        };
    }

    // ── Workflow ──────────────────────────────────────────────────────────────

    public async Task<bool> CompleteCheckAsync(Guid checkId, Guid completedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _checkRepository.GetWithItemsAsync(checkId);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Pre-employment check with ID '{checkId}' not found.");

        if (entity.OverallStatus is PreEmploymentCheckStatus.Completed or PreEmploymentCheckStatus.Failed)
            throw new InvalidOperationException("This pre-employment check has already been completed.");

        var live = entity.Items.Where(i => !i.IsDeleted).ToList();

        // ⚠ "Not yet done" is not "failed". The rule was `IsBlockingOnFail && Passed != true`, which
        // treats a blocking item still sitting Pending as a failure — so completing a check while
        // waiting on a police report marked the whole thing Failed. Failed is terminal, there is no
        // reopen, and the offer could then never reach ChecksCleared. An outstanding item now
        // refuses the completion instead, which is recoverable.
        var outstanding = live
            .Where(i => (i.IsMandatory || i.IsBlockingOnFail)
                        && i.Status is CheckItemStatus.Pending or CheckItemStatus.Requested)
            .ToList();
        if (outstanding.Count > 0)
            throw new InvalidOperationException(
                $"{outstanding.Count} mandatory or blocking check(s) are still outstanding: " +
                string.Join(", ", outstanding.Select(i => i.CheckType.ToString())) +
                ". Record their results before completing.");

        // ⚠ The same rule failed a check on a WAIVED or NOT-APPLICABLE blocking item, because
        // neither sets `Passed = true`. Waiving a check is a decision to accept it, not a failure —
        // otherwise the waiver facility guarantees the outcome it exists to avoid.
        var hasBlockingFailures = live.Any(i =>
            i.IsBlockingOnFail
            && i.Status is not (CheckItemStatus.Waived or CheckItemStatus.NotApplicable)
            && i.Passed != true);
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

        var template = await _templateRepository.GetByIdWithItemsAsync(templateId, check.TenantId);
        if (template == null)
            throw new ArgumentException($"Pre-employment check template '{templateId}' not found.");

        var existingByType = check.Items
            .Where(i => !i.IsDeleted)
            .GroupBy(i => i.CheckType)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var templateItem in template.Items)
        {
            var alreadyPresent = existingByType.TryGetValue(templateItem.CheckType, out var existingItems);

            if (alreadyPresent && !overwriteExisting)
                continue;

            // ⚠ `overwriteExisting` used only to skip the `continue`, so it ADDED a second item of
            // the same check type rather than overwriting — the flag's name described behaviour it
            // did not have, and re-applying a template quietly doubled every item on the check.
            // A completed item is left alone whatever the flag says: overwriting it would discard a
            // result somebody has already obtained.
            if (alreadyPresent && overwriteExisting)
            {
                foreach (var stale in existingItems!.Where(i => i.Status == CheckItemStatus.Pending))
                {
                    stale.ServiceProviderName = templateItem.DefaultServiceProvider;
                    stale.ServiceProviderSupplierId = templateItem.DefaultServiceProviderSupplierId;
                    stale.Instructions        = templateItem.Instructions;
                    stale.IsMandatory         = templateItem.IsMandatory;
                    stale.IsBlockingOnFail    = templateItem.IsBlockingOnFail;
                    stale.ExpectedDays        = templateItem.ExpectedDays;
                    stale.UpdatedAt           = DateTime.UtcNow;
                    stale.UpdatedBy           = appliedByUserId.ToString();
                    await _itemRepository.UpdateAsync(stale);
                }
                continue;
            }

            var newItem = new PreEmploymentCheckItem
            {
                TenantId = check.TenantId,
                PreEmploymentCheckId = check.Id,
                CheckType = templateItem.CheckType,
                ServiceProviderName = templateItem.DefaultServiceProvider,
                ServiceProviderSupplierId = templateItem.DefaultServiceProviderSupplierId,
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

/// <summary>
/// Reusable pre-employment check templates — "which checks a hire of this kind needs".
///
/// <para>⚠ <b>This service had no tenancy of any kind.</b> It took no <c>ICurrentUserProvider</c>,
/// and its repository's reads carried no <c>TenantId</c> predicate, so the list returned every
/// tenant's templates and every by-id operation — read, update, delete, add/update/remove item —
/// acted on whichever tenant happened to own that id. <c>DeleteItemAsync</c> was the clearest case:
/// it loaded every template in the database and soft-deleted the matching item.</para>
///
/// <para>The <c>ApplicationDbContext</c> is registered without a tenant, so the global query filter
/// is inert and could never have caught this. Scoping is explicit here, as everywhere else in HR.</para>
/// </summary>
public class PreEmploymentCheckTemplateService : IPreEmploymentCheckTemplateService
{
    private readonly IPreEmploymentCheckTemplateRepository _templateRepository;
    private readonly ErpSystem.Core.Interfaces.Procurement.ISupplierRepository _suppliers;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PreEmploymentCheckTemplateService> _logger;

    public PreEmploymentCheckTemplateService(
        IPreEmploymentCheckTemplateRepository templateRepository,
        ErpSystem.Core.Interfaces.Procurement.ISupplierRepository suppliers,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<PreEmploymentCheckTemplateService> logger)
    {
        _templateRepository = templateRepository;
        _suppliers = suppliers;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    // A template owned by another tenant is reported as missing rather than forbidden, so the
    // endpoints do not confirm that the id exists elsewhere.
    private async Task<PreEmploymentCheckTemplate> GetOwnedTemplateAsync(Guid id)
    {
        var entity = await _templateRepository.GetByIdWithItemsAsync(id, GetTenantId());
        if (entity == null)
            throw new ArgumentException($"Pre-employment check template '{id}' not found.");
        return entity;
    }

    public async Task<IEnumerable<PreEmploymentCheckTemplateDto>> GetAllAsync(CancellationToken ct = default)
    {
        var entities = await _templateRepository.GetAllActiveAsync(GetTenantId());
        return entities.Select(e => e.ToDto());
    }

    public async Task<PreEmploymentCheckTemplateDetailDto> GetWithItemsAsync(Guid id, CancellationToken ct = default)
        => (await GetOwnedTemplateAsync(id)).ToDetailDto();

    public async Task<PreEmploymentCheckTemplateDetailDto> CreateAsync(CreatePreEmploymentCheckTemplateDto dto, Guid tenantId, Guid createdByUserId, CancellationToken ct = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        var entity = dto.ToEntity(current, createdByUserId);
        await _templateRepository.AddAsync(entity);

        foreach (var itemDto in dto.Items)
        {
            itemDto.TemplateId = entity.Id;
            var item = itemDto.ToEntity(current, createdByUserId);
            entity.Items.Add(item);
        }

        await _unitOfWork.SaveChangesAsync(ct);
        _logger.LogInformation("Pre-employment check template '{Name}' created", entity.Name);

        return (await _templateRepository.GetByIdWithItemsAsync(entity.Id, current))!.ToDetailDto();
    }

    public async Task<PreEmploymentCheckTemplateDto> UpdateAsync(UpdatePreEmploymentCheckTemplateDto dto, Guid updatedByUserId, CancellationToken ct = default)
    {
        var entity = await GetOwnedTemplateAsync(dto.Id);

        entity.UpdateEntity(dto, updatedByUserId);
        await _templateRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await GetOwnedTemplateAsync(id);

        await _templateRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }

    public async Task<PreEmploymentCheckTemplateItemDto> AddItemAsync(CreatePreEmploymentCheckTemplateItemDto dto, Guid tenantId, Guid createdByUserId, CancellationToken ct = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        var template = await GetOwnedTemplateAsync(dto.TemplateId);

        // A template listing the same check twice would seed two identical items onto every check
        // built from it.
        if (template.Items.Any(i => i.CheckType == dto.CheckType))
            throw new InvalidOperationException("That check type is already on this template.");
        dto.DefaultServiceProvider = await ResolveDefaultProviderNameAsync(dto.DefaultServiceProviderSupplierId, dto.DefaultServiceProvider, current);

        var item = dto.ToEntity(current, createdByUserId);
        await _templateRepository.AddTemplateItemAsync(item);
        await _unitOfWork.SaveChangesAsync(ct);
        return item.ToDto();
    }

    public async Task<PreEmploymentCheckTemplateItemDto> UpdateItemAsync(UpdatePreEmploymentCheckTemplateItemDto dto, Guid updatedByUserId, CancellationToken ct = default)
    {
        var template = await GetOwnedTemplateAsync(dto.TemplateId);

        var item = template.Items.FirstOrDefault(i => i.Id == dto.Id)
            ?? throw new ArgumentException($"Template item '{dto.Id}' not found on template '{dto.TemplateId}'.");

        // No duplicate-check-type guard here, unlike AddItemAsync: UpdatePreEmploymentCheckTemplateItemDto
        // deliberately omits CheckType ("delete + re-add to change it"), so an update cannot
        // introduce a clash.
        dto.DefaultServiceProvider = await ResolveDefaultProviderNameAsync(dto.DefaultServiceProviderSupplierId, dto.DefaultServiceProvider, GetTenantId());
        item.UpdateEntity(dto, updatedByUserId);
        await _templateRepository.UpdateAsync(template);
        await _unitOfWork.SaveChangesAsync(ct);
        return item.ToDto();
    }

    /// <summary>Round 3, lane G (D-14): a named supplier must be the tenant's; its name is mirrored into the snapshot.</summary>
    private async Task<string?> ResolveDefaultProviderNameAsync(Guid? supplierId, string? typedName, Guid tenantId)
    {
        if (supplierId is not { } id) return string.IsNullOrWhiteSpace(typedName) ? null : typedName.Trim();
        var supplier = await _suppliers.GetQueryable().AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId && !x.IsDeleted);
        if (supplier is null)
            throw new InvalidOperationException("That service provider is not a supplier of this organisation. Pick one from the supplier register.");
        return supplier.Name;
    }

    /// <summary>
    /// ⚠ Takes the owning template id as well as the item id. The previous implementation loaded
    /// <b>every tenant's</b> templates and soft-deleted whichever item matched, which is how a
    /// bare item id became a cross-tenant delete.
    /// </summary>
    public async Task<bool> DeleteItemAsync(Guid templateId, Guid itemId, CancellationToken ct = default)
    {
        var template = await GetOwnedTemplateAsync(templateId);

        var item = template.Items.FirstOrDefault(i => i.Id == itemId)
            ?? throw new ArgumentException($"Template item '{itemId}' not found on template '{templateId}'.");

        item.IsDeleted = true;
        item.UpdatedAt = DateTime.UtcNow;
        await _templateRepository.UpdateAsync(template);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }
}
