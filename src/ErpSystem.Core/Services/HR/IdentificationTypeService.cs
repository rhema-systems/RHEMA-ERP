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
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<IdentificationTypeService> _logger;

    public IdentificationTypeService(
        IGenericRepository<IdentificationType> identificationTypeRepository,
        IUnitOfWork unitOfWork,
        ILogger<IdentificationTypeService> logger)
    {
        _identificationTypeRepository = identificationTypeRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<IdentificationTypeDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var identificationType = await _identificationTypeRepository.GetQueryable()
            .Include(it => it.IssuingCountry)
            .FirstOrDefaultAsync(it => it.Id == id, cancellationToken);

        if (identificationType == null)
        {
            throw new ArgumentException($"Identification type with ID '{id}' not found.");
        }

        return identificationType.ToDto();
    }

    public async Task<IEnumerable<IdentificationTypeDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var identificationTypes = await _identificationTypeRepository.GetQueryable()
            .Include(it => it.IssuingCountry)
            .OrderBy(it => it.Name)
            .ToListAsync(cancellationToken);

        return identificationTypes.ToDtoList();
    }

    public async Task<IEnumerable<IdentificationTypeDto>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        var identificationTypes = await _identificationTypeRepository.GetQueryable()
            .Include(it => it.IssuingCountry)
            .Where(it => it.IsActive)
            .OrderBy(it => it.Name)
            .ToListAsync(cancellationToken);

        return identificationTypes.ToDtoList();
    }

    public async Task<PagedResult<IdentificationTypeDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _identificationTypeRepository.GetQueryable()
            .Include(it => it.IssuingCountry)
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
        // Validate uniqueness of code if provided
        if (!string.IsNullOrWhiteSpace(createDto.Code))
        {
            var existingWithCode = await _identificationTypeRepository.GetQueryable()
                .AnyAsync(it => it.Code == createDto.Code, cancellationToken);

            if (existingWithCode)
            {
                throw new InvalidOperationException($"An identification type with code '{createDto.Code}' already exists.");
            }
        }

        var identificationType = createDto.ToEntity();
        await _identificationTypeRepository.AddAsync(identificationType);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(identificationType.Id, cancellationToken);
    }

    public async Task<IdentificationTypeDto> UpdateAsync(UpdateIdentificationTypeDto updateDto, CancellationToken cancellationToken = default)
    {
        var identificationType = await _identificationTypeRepository.GetQueryable()
            .FirstOrDefaultAsync(it => it.Id == updateDto.Id, cancellationToken);

        if (identificationType == null)
        {
            throw new ArgumentException($"Identification type with ID '{updateDto.Id}' not found.");
        }

        // Validate uniqueness of code if provided and changed
        if (!string.IsNullOrWhiteSpace(updateDto.Code) && updateDto.Code != identificationType.Code)
        {
            var existingWithCode = await _identificationTypeRepository.GetQueryable()
                .AnyAsync(it => it.Code == updateDto.Code && it.Id != updateDto.Id, cancellationToken);

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

        if (identificationType == null)
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
        var identificationType = await _identificationTypeRepository.GetByIdAsync(id);

        if (identificationType == null)
        {
            throw new ArgumentException($"Identification type with ID '{id}' not found.");
        }

        identificationType.IsActive = true;
        await _identificationTypeRepository.UpdateAsync(identificationType);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> DeactivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var identificationType = await _identificationTypeRepository.GetByIdAsync(id);

        if (identificationType == null)
        {
            throw new ArgumentException($"Identification type with ID '{id}' not found.");
        }

        identificationType.IsActive = false;
        await _identificationTypeRepository.UpdateAsync(identificationType);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}
