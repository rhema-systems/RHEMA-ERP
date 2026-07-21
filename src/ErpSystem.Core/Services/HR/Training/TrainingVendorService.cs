using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

public class TrainingVendorService : ITrainingVendorService
{
    private readonly ITrainingVendorRepository _vendorRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TrainingVendorService> _logger;

    public TrainingVendorService(
        ITrainingVendorRepository vendorRepository,
        IUnitOfWork unitOfWork,
        ILogger<TrainingVendorService> logger)
    {
        _vendorRepository = vendorRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // ── Queries ──────────────────────────────────────────────────────────────

    public async Task<TrainingVendorDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _vendorRepository.GetWithFullDetailsAsync(id);

        if (entity == null)
            throw new ArgumentException($"Training vendor with ID '{id}' not found.");

        return entity.ToDto();
    }

    public async Task<TrainingVendorDto?> GetByVendorCodeAsync(string vendorCode, CancellationToken cancellationToken = default)
    {
        var entity = await _vendorRepository.GetByVendorCodeAsync(vendorCode);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<TrainingVendorSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _vendorRepository.GetAllAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<PagedResult<TrainingVendorSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _vendorRepository.GetQueryable();
        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(v => v.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<TrainingVendorSummaryDto>
        {
            Items = items.ToSummaryDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<IEnumerable<TrainingVendorSummaryDto>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _vendorRepository.GetActiveVendorsAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<TrainingVendorSummaryDto>> GetPreferredAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _vendorRepository.GetPreferredVendorsAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<TrainingVendorSummaryDto>> GetBlacklistedAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _vendorRepository.GetBlacklistedVendorsAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<TrainingVendorSummaryDto>> GetWithExpiringAccreditationAsync(int daysAhead = 30, CancellationToken cancellationToken = default)
    {
        var entities = await _vendorRepository.GetWithExpiringAccreditationAsync(daysAhead);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<TrainingVendorSummaryDto>> GetByVendorTypeAsync(TrainingVendorType vendorType, CancellationToken cancellationToken = default)
    {
        var entities = await _vendorRepository.GetByVendorTypeAsync(vendorType);
        return entities.ToSummaryDtoList();
    }

    // ── CRUD ─────────────────────────────────────────────────────────────────

    public async Task<TrainingVendorDto> CreateAsync(CreateTrainingVendorDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);

        await _vendorRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training vendor created: {VendorCode} — {Name}", entity.VendorCode, entity.Name);

        return entity.ToDto();
    }

    public async Task<TrainingVendorDto> UpdateAsync(UpdateTrainingVendorDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _vendorRepository.GetByIdAsync(updateDto.Id);

        if (entity == null)
            throw new ArgumentException($"Training vendor with ID '{updateDto.Id}' not found.");

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _vendorRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training vendor updated: {VendorCode}", entity.VendorCode);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _vendorRepository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Training vendor with ID '{id}' not found.");

        await _vendorRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training vendor deleted: {VendorCode}", entity.VendorCode);

        return true;
    }

    // ── Workflow ──────────────────────────────────────────────────────────────

    public async Task<bool> BlacklistVendorAsync(BlacklistVendorDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _vendorRepository.GetByIdAsync(dto.VendorId);

        if (entity == null)
            throw new ArgumentException($"Training vendor with ID '{dto.VendorId}' not found.");

        if (entity.IsBlacklisted)
            throw new InvalidOperationException("Vendor is already blacklisted.");

        entity.IsBlacklisted = true;
        entity.BlacklistedDate = DateTime.UtcNow;
        entity.BlacklistReason = dto.BlacklistReason;
        entity.IsActive = false;
        entity.IsPreferred = false;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = updatedByUserId.ToString();

        await _vendorRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training vendor blacklisted: {VendorCode} — Reason: {Reason}", entity.VendorCode, dto.BlacklistReason);

        return true;
    }

    public async Task<bool> UnblacklistVendorAsync(Guid vendorId, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _vendorRepository.GetByIdAsync(vendorId);

        if (entity == null)
            throw new ArgumentException($"Training vendor with ID '{vendorId}' not found.");

        if (!entity.IsBlacklisted)
            throw new InvalidOperationException("Vendor is not currently blacklisted.");

        entity.IsBlacklisted = false;
        entity.BlacklistedDate = null;
        entity.BlacklistReason = null;
        entity.IsActive = true;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = updatedByUserId.ToString();

        await _vendorRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training vendor removed from blacklist: {VendorCode}", entity.VendorCode);

        return true;
    }
}
