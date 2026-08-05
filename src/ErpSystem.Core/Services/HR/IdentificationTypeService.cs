using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Services.HR.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

public class IdentificationTypeService : IIdentificationTypeService
{
    private readonly IGenericRepository<IdentificationType> _identificationTypeRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<IdentificationTypeService> _logger;

    public IdentificationTypeService(
        IGenericRepository<IdentificationType> identificationTypeRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<IdentificationTypeService> logger)
    {
        _identificationTypeRepository = identificationTypeRepository;
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

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    private async Task<IdentificationType> GetOwnedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var identificationType = await _identificationTypeRepository.GetQueryable()
            .Include(it => it.IssuingCountry)
            .FirstOrDefaultAsync(it => it.Id == id, cancellationToken);

        if (identificationType == null || identificationType.TenantId != GetTenantId())
        {
            throw new ArgumentException($"Identification type with ID '{id}' not found.");
        }

        return identificationType;
    }

    public async Task<IdentificationTypeDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var identificationType = await GetOwnedAsync(id, cancellationToken);
        return identificationType.ToDto();
    }

    public async Task<IEnumerable<IdentificationTypeDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var identificationTypes = await _identificationTypeRepository.GetQueryable()
            .Include(it => it.IssuingCountry)
            .Where(it => it.TenantId == tenantId)
            .OrderBy(it => it.Name)
            .ToListAsync(cancellationToken);

        return identificationTypes.ToDtoList();
    }

    public async Task<IEnumerable<IdentificationTypeDto>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var identificationTypes = await _identificationTypeRepository.GetQueryable()
            .Include(it => it.IssuingCountry)
            .Where(it => it.TenantId == tenantId && it.IsActive)
            .OrderBy(it => it.Name)
            .ToListAsync(cancellationToken);

        return identificationTypes.ToDtoList();
    }

    public async Task<PagedResult<IdentificationTypeDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var query = _identificationTypeRepository.GetQueryable()
            .Include(it => it.IssuingCountry)
            .Where(it => it.TenantId == tenantId)
            .OrderBy(it => it.Name);

        var totalCount = await query.CountAsync(cancellationToken);

        var identificationTypes = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<IdentificationTypeDto>
        {
            Items = identificationTypes.ToDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<IdentificationTypeDto> CreateAsync(CreateIdentificationTypeDto createDto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        // Codes are unique per tenant: an unscoped check would let one tenant's codes block another's.
        if (!string.IsNullOrWhiteSpace(createDto.Code))
        {
            var existingWithCode = await _identificationTypeRepository.GetQueryable()
                .AnyAsync(it => it.TenantId == tenantId && it.Code == createDto.Code, cancellationToken);

            if (existingWithCode)
            {
                throw new InvalidOperationException($"An identification type with code '{createDto.Code}' already exists.");
            }
        }

        var identificationType = createDto.ToEntity();
        identificationType.TenantId = tenantId;
        await _identificationTypeRepository.AddAsync(identificationType);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(identificationType.Id, cancellationToken);
    }

    public async Task<IdentificationTypeDto> UpdateAsync(UpdateIdentificationTypeDto updateDto, CancellationToken cancellationToken = default)
    {
        var identificationType = await _identificationTypeRepository.GetQueryable()
            .FirstOrDefaultAsync(it => it.Id == updateDto.Id, cancellationToken);

        if (identificationType == null || identificationType.TenantId != GetTenantId())
        {
            throw new ArgumentException($"Identification type with ID '{updateDto.Id}' not found.");
        }

        // Codes are unique per tenant: an unscoped check would let one tenant's codes block another's.
        if (!string.IsNullOrWhiteSpace(updateDto.Code) && updateDto.Code != identificationType.Code)
        {
            var existingWithCode = await _identificationTypeRepository.GetQueryable()
                .AnyAsync(it => it.TenantId == identificationType.TenantId && it.Code == updateDto.Code && it.Id != updateDto.Id, cancellationToken);

            if (existingWithCode)
            {
                throw new InvalidOperationException($"An identification type with code '{updateDto.Code}' already exists.");
            }
        }

        updateDto.UpdateEntity(identificationType);
        await _identificationTypeRepository.UpdateAsync(identificationType);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(identificationType.Id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var identificationType = await _identificationTypeRepository.GetQueryable()
            .Include(it => it.EmployeeIdentificationCards)
            .FirstOrDefaultAsync(it => it.Id == id, cancellationToken);

        if (identificationType == null || identificationType.TenantId != GetTenantId())
        {
            throw new ArgumentException($"Identification type with ID '{id}' not found.");
        }

        // Check if any employee identification cards are using this type
        if (identificationType.EmployeeIdentificationCards.Any())
        {
            throw new InvalidOperationException($"Cannot delete identification type '{identificationType.Name}' because it is being used by {identificationType.EmployeeIdentificationCards.Count} employee identification card(s). Consider deactivating it instead.");
        }

        await _identificationTypeRepository.DeleteAsync(identificationType);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> ActivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var identificationType = await GetOwnedAsync(id, cancellationToken);

        identificationType.IsActive = true;
        await _identificationTypeRepository.UpdateAsync(identificationType);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> DeactivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var identificationType = await GetOwnedAsync(id, cancellationToken);

        identificationType.IsActive = false;
        await _identificationTypeRepository.UpdateAsync(identificationType);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}
