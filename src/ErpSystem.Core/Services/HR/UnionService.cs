using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Services.HR.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

public class UnionService : IUnionService
{
    private readonly IUnionRepository _unionRepository;
    private readonly ICollectiveBargainingAgreementRepository _agreementRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UnionService> _logger;

    public UnionService(
        IUnionRepository unionRepository,
        ICollectiveBargainingAgreementRepository agreementRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<UnionService> logger)
    {
        _unionRepository = unionRepository;
        _agreementRepository = agreementRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly and passes it into the repository predicate.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private async Task<Union> GetOwnedUnionAsync(Guid id)
    {
        var entity = await _unionRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Union with ID '{id}' not found.");
        return entity;
    }

    /// <summary>
    /// The union with its agreements loaded — what every path that MAPS a union must use.
    /// </summary>
    /// <remarks>
    /// ⚠ This is D-6, measured rather than argued. <c>UpdateAsync</c> mapped the entity returned by
    /// <c>GetOwnedUnionAsync</c>, which fetches without the agreements, so the PUT response carried
    /// <c>agreementCount: 0</c> and an empty list for a union the GET reported two agreements for. A
    /// screen that re-renders from its own save response therefore emptied the agreements table in
    /// front of the user, and a refresh brought them back. The plain <c>GetOwnedUnionAsync</c> stays
    /// for the paths that only need to prove ownership.
    /// </remarks>
    private async Task<Union> LoadOwnedUnionWithAgreementsAsync(Guid id)
    {
        var entity = await _unionRepository.GetByIdWithAgreementsAsync(id, GetTenantId());
        if (entity == null)
            throw new ArgumentException($"Union with ID '{id}' not found.");
        return entity;
    }

    /// <summary>The agreement with its union loaded, so the mapped DTO can name it.</summary>
    /// <remarks>
    /// ⚠ The same shape as D-6, one level down. <c>AddAgreementAsync</c> gets away with the plain
    /// fetch only by accident: it loads the union first to check ownership, so EF's navigation fixup
    /// fills <c>entity.Union</c> for free. <c>UpdateAgreementAsync</c> loads no union, so its
    /// response came back with <c>unionName: null</c> while the GET beside it carried the name.
    /// </remarks>
    private async Task<CollectiveBargainingAgreement> GetOwnedAgreementAsync(Guid id)
    {
        var entity = await _agreementRepository.GetByIdWithUnionAsync(id, GetTenantId());
        if (entity == null)
            throw new ArgumentException($"Collective bargaining agreement with ID '{id}' not found.");
        return entity;
    }

    /// <summary>
    /// An agreement cannot expire before it takes effect.
    /// </summary>
    /// <remarks>
    /// Nothing checked this, and the register's whole job is to say which agreement is in force —
    /// a backwards pair makes that question unanswerable rather than merely wrong.
    /// </remarks>
    private static void ValidateAgreementDates(DateTime effectiveDate, DateTime? expiryDate)
    {
        if (expiryDate.HasValue && expiryDate.Value.Date < effectiveDate.Date)
            throw new InvalidOperationException(
                "The agreement's expiry date cannot be earlier than its effective date.");
    }

    // ⚠ The tenant predicate is now inside the query rather than applied to the result. All three
    // reads used to fetch every tenant's unions WITH their whole agreement graphs and discard most of
    // them in memory. Never a leak — the filter did run — but the wrong place for it.
    public async Task<IEnumerable<UnionDto>> GetAllAsync(CancellationToken cancellationToken = default)
        => (await _unionRepository.GetAllWithCountsAsync(GetTenantId())).ToDtoList();

    public async Task<IEnumerable<UnionDto>> GetActiveAsync(CancellationToken cancellationToken = default)
        => (await _unionRepository.GetActiveAsync(GetTenantId())).ToDtoList();

    public async Task<UnionDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => (await LoadOwnedUnionWithAgreementsAsync(id)).ToDto();

    public async Task<UnionDto> CreateAsync(CreateUnionDto createDto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        if (!string.IsNullOrWhiteSpace(createDto.Code))
        {
            // Codes are unique per tenant: an unscoped check would let one tenant's codes block another's.
            var duplicate = await _unionRepository.GetQueryable()
                .AnyAsync(u => u.TenantId == tenantId && u.Code == createDto.Code, cancellationToken);
            if (duplicate)
                throw new InvalidOperationException($"A union with code '{createDto.Code}' already exists.");
        }

        var entity = createDto.ToEntity();
        entity.TenantId = tenantId;
        await _unionRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Union created: {Name}", entity.Name);
        return entity.ToDto();
    }

    public async Task<UnionDto> UpdateAsync(UpdateUnionDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedUnionAsync(updateDto.Id);

        if (!string.IsNullOrWhiteSpace(updateDto.Code) && updateDto.Code != entity.Code)
        {
            var duplicate = await _unionRepository.GetQueryable()
                .AnyAsync(u => u.TenantId == entity.TenantId && u.Code == updateDto.Code && u.Id != updateDto.Id, cancellationToken);
            if (duplicate)
                throw new InvalidOperationException($"A union with code '{updateDto.Code}' already exists.");
        }

        updateDto.UpdateEntity(entity);
        await _unionRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Union updated: {Name}", entity.Name);

        // Re-read with the agreements so the write response says what the read says. See the remarks
        // on LoadOwnedUnionWithAgreementsAsync.
        return (await LoadOwnedUnionWithAgreementsAsync(entity.Id)).ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await LoadOwnedUnionWithAgreementsAsync(id);

        // ⚠ Deleting a union is a soft delete, and the cascade configured on the relationship only
        // fires on a hard one. So this used to leave the agreements alive and unreachable: every read
        // of them goes through the union, which no longer resolves. Refusing is the same answer
        // OrganizationUnitService gives for a unit with children, and it names the number so the
        // caller knows what to clear first.
        var agreementCount = entity.Agreements?.Count ?? 0;
        if (agreementCount > 0)
            throw new InvalidOperationException(
                $"Cannot delete this union because it has {agreementCount} collective bargaining agreement(s). Remove them first.");

        await _unionRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Union deleted: {Id}", id);
        return true;
    }

    public async Task<CollectiveBargainingAgreementDto> AddAgreementAsync(CreateCollectiveBargainingAgreementDto createDto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        await GetOwnedUnionAsync(createDto.UnionId);
        ValidateAgreementDates(createDto.EffectiveDate, createDto.ExpiryDate);

        var entity = createDto.ToEntity();
        entity.TenantId = tenantId;
        await _agreementRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("CBA added to union: {UnionId}", createDto.UnionId);
        return entity.ToDto();
    }

    public async Task<IEnumerable<CollectiveBargainingAgreementDto>> GetAgreementsAsync(Guid unionId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        await GetOwnedUnionAsync(unionId);
        return (await _agreementRepository.GetByUnionIdAsync(unionId, tenantId)).ToDtoList();
    }

    public async Task<CollectiveBargainingAgreementDto> UpdateAgreementAsync(UpdateCollectiveBargainingAgreementDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAgreementAsync(updateDto.Id);
        ValidateAgreementDates(updateDto.EffectiveDate, updateDto.ExpiryDate);
        updateDto.UpdateEntity(entity);
        await _agreementRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("CBA updated: {Id}", updateDto.Id);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAgreementAsync(Guid agreementId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAgreementAsync(agreementId);
        await _agreementRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("CBA deleted: {Id}", agreementId);
        return true;
    }
}
