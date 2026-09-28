using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Training;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

public class TrainingVendorService : ITrainingVendorService
{
    private readonly ITrainingVendorRepository _vendorRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TrainingVendorService> _logger;

    public TrainingVendorService(
        ITrainingVendorRepository vendorRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<TrainingVendorService> logger)
    {
        _vendorRepository = vendorRepository;
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

    // A vendor owned by another tenant is reported as missing rather than forbidden, so the endpoints do
    // not confirm that the id exists elsewhere.
    private async Task<TrainingVendor> GetOwnedAsync(Guid id)
    {
        var entity = await _vendorRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Training vendor with ID '{id}' not found.");
        return entity;
    }

    // ── Queries ──────────────────────────────────────────────────────────────

    public async Task<TrainingVendorDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _vendorRepository.GetWithFullDetailsAsync(id);

        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"Training vendor with ID '{id}' not found.");

        return entity.ToDto();
    }

    public async Task<TrainingVendorDto?> GetByVendorCodeAsync(string vendorCode, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _vendorRepository.GetByVendorCodeAsync(vendorCode);

        // Vendor codes are unique per tenant, so a match owned by another tenant is reported as no match.
        return entity != null && entity.TenantId == tenantId ? entity.ToDto() : null;
    }

    public async Task<IEnumerable<TrainingVendorSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _vendorRepository.GetAllAsync();
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<PagedResult<TrainingVendorSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        var query = _vendorRepository.GetQueryable().Where(v => v.TenantId == tenantId);
        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            // The summary DTO counts this collection; without the include it reads 0 on every row.
            .Include(v => v.Trainers)
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
        var tenantId = GetTenantId();
        var entities = await _vendorRepository.GetActiveVendorsAsync();
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<TrainingVendorSummaryDto>> GetPreferredAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _vendorRepository.GetPreferredVendorsAsync();
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<TrainingVendorSummaryDto>> GetBlacklistedAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _vendorRepository.GetBlacklistedVendorsAsync();
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<TrainingVendorSummaryDto>> GetWithExpiringAccreditationAsync(int daysAhead = 30, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _vendorRepository.GetWithExpiringAccreditationAsync(daysAhead);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<TrainingVendorSummaryDto>> GetByVendorTypeAsync(TrainingVendorType vendorType, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _vendorRepository.GetByVendorTypeAsync(vendorType);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    // ── CRUD ─────────────────────────────────────────────────────────────────

    public async Task<TrainingVendorDto> CreateAsync(CreateTrainingVendorDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        // Codes are unique per tenant: an unscoped check would let one tenant's codes block another's.
        var duplicate = await _vendorRepository.GetQueryable()
            .AnyAsync(v => v.TenantId == current && v.VendorCode == createDto.VendorCode, cancellationToken);
        if (duplicate)
            throw new InvalidOperationException($"A training vendor with code '{createDto.VendorCode}' already exists.");

        var entity = createDto.ToEntity(current, createdByUserId);

        await _vendorRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training vendor created: {VendorCode} — {Name}", entity.VendorCode, entity.Name);

        return entity.ToDto();
    }

    public async Task<TrainingVendorDto> UpdateAsync(UpdateTrainingVendorDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(updateDto.Id);

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _vendorRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training vendor updated: {VendorCode}", entity.VendorCode);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);

        // Round 4, lane M3: orientation sessions can name a vendor as their facilitator.
        if (await _unitOfWork.WhyStillBookedAsync(entity.TenantId, entity.Id, null, entity.Name,
                "make the vendor inactive, which keeps its record and stops it being picked", cancellationToken) is { } booked)
            throw new InvalidOperationException(booked);

        await _vendorRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training vendor deleted: {VendorCode}", entity.VendorCode);

        return true;
    }

    // ── Workflow ──────────────────────────────────────────────────────────────

    public async Task<bool> BlacklistVendorAsync(BlacklistVendorDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(dto.VendorId);

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
        var entity = await GetOwnedAsync(vendorId);

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
